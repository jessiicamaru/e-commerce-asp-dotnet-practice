using System.Text.Json;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Application.Email;

/// <summary>Sends the emails due by <paramref name="Now"/> (specs/060). Returns how many went out.</summary>
public record DispatchEmailsCommand(DateTime Now) : IRequest<int>;

/// <summary>
/// Claim the due emails, render each in its language, hand it to the mail server, and record what happened -
/// all under the claim's lock, so two instances never send one row.
/// </summary>
/// <remarks>
/// <para>
/// A mail server that refuses or is down pushes the email back - 1 minute, doubling to at most 1 hour - and
/// after <see cref="EmailOptions.MaxAttempts"/> it is <c>Failed</c> with its last error. Delayed, not lost:
/// the row survives a restart, which an in-memory retry on the consumer would not.
/// </para>
/// <para>
/// Sending inside the claim means a commit that fails after a successful send sends that one email twice.
/// That is at-least-once, and the accepted price: marking it sent BEFORE sending loses it whenever the send
/// then fails.
/// </para>
/// </remarks>
public class DispatchEmailsCommandHandler(
    IOutgoingEmailRepository emails,
    IUserRepository users,
    IEmailTransport transport,
    IUnitOfWork unitOfWork,
    IOptions<EmailOptions> options,
    ILogger<DispatchEmailsCommandHandler> logger) : IRequestHandler<DispatchEmailsCommand, int>
{
    public static readonly TimeSpan FirstRetry = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan LongestRetry = TimeSpan.FromHours(1);

    private readonly IOutgoingEmailRepository _emails = emails;
    private readonly IUserRepository _users = users;
    private readonly IEmailTransport _transport = transport;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly EmailOptions _options = options.Value;
    private readonly ILogger<DispatchEmailsCommandHandler> _logger = logger;

    public async Task<int> Handle(DispatchEmailsCommand request, CancellationToken cancellationToken)
    {
        var sent = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            sent = 0;
            foreach (var email in await _emails.ClaimDueAsync(request.Now, _options.Batch, ct))
            {
                if (await SendOneAsync(email, request.Now, ct))
                {
                    sent++;
                }
            }

            await _emails.SaveChangesAsync(ct);
        }, cancellationToken);

        return sent;
    }

    private async Task<bool> SendOneAsync(OutgoingEmail email, DateTime now, CancellationToken ct)
    {
        var recipient = await _users.GetByIdAsync(email.RecipientId, ct);
        if (recipient is null)
        {
            // Nobody to write to, and retrying will not create them.
            Fail(email, "No such recipient.");
            return false;
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(email.DataJson) ?? [];
        var rendered = EmailTemplates.Render(email.Template, email.Language, data, recipient.FirstName, _options.StorefrontUrl);
        if (rendered is null)
        {
            Fail(email, $"No words for template '{email.Template}', or its data is incomplete.");
            return false;
        }

        try
        {
            await _transport.SendAsync(recipient.Email, rendered.Subject, rendered.Body, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            email.Attempts++;
            email.LastError = Truncate(exception.Message);

            if (email.Attempts >= _options.MaxAttempts)
            {
                email.Status = OutgoingEmailStatus.Failed;
                _logger.LogError(exception, "Email {EmailId} ({Template}) gave up after {Attempts} attempts.",
                    email.Id, email.Template, email.Attempts);
            }
            else
            {
                email.NextAttemptAt = now + Backoff(email.Attempts);
                _logger.LogWarning("Email {EmailId} ({Template}) could not be sent, attempt {Attempts}; next at {Next:o}: {Error}",
                    email.Id, email.Template, email.Attempts, email.NextAttemptAt, email.LastError);
            }

            return false;
        }

        email.Status = OutgoingEmailStatus.Sent;
        email.SentAt = now;
        email.Attempts++;
        email.LastError = null;
        return true;
    }

    /// <summary>1, 2, 4, 8 ... minutes, never more than an hour.</summary>
    public static TimeSpan Backoff(int attempts)
    {
        var minutes = FirstRetry.TotalMinutes * Math.Pow(2, Math.Max(0, attempts - 1));
        return TimeSpan.FromMinutes(Math.Min(minutes, LongestRetry.TotalMinutes));
    }

    private void Fail(OutgoingEmail email, string reason)
    {
        email.Status = OutgoingEmailStatus.Failed;
        email.Attempts++;
        email.LastError = reason;
        _logger.LogWarning("Email {EmailId} ({Template}) will not be sent: {Reason}", email.Id, email.Template, reason);
    }

    private static string Truncate(string text) => text.Length <= 1000 ? text : text[..1000];
}
