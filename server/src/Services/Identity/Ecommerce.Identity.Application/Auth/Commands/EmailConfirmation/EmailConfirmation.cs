using System.Text.Json;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Email;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.EmailConfirmation;

/// <summary>Uses the link sent to an address (specs/063). Anonymous: the link may be opened in another browser.</summary>
public record ConfirmEmailCommand(string Token) : IRequest;

/// <summary>Sends the caller a new link (specs/063). The account comes from the token, never the body.</summary>
/// <param name="Language">What the email is written in; the controller reads it from <c>Accept-Language</c>.</param>
public record ResendConfirmationCommand(string Language = "") : IRequest;

/// <summary>The links sent to confirm addresses, kept as hashes (specs/063).</summary>
public interface IEmailConfirmationRepository
{
    /// <summary>Stages a link: it is written with the caller's next save.</summary>
    Task AddAsync(EmailConfirmationToken token, CancellationToken cancellationToken = default);

    /// <summary>Removes the person's links that were never used: the newest one is the only way in.</summary>
    Task DeleteUnusedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a link was sent to this person since <paramref name="since"/>. Locks the person's row first, so
    /// two requests at once are decided one after the other. Call inside the transaction.
    /// </summary>
    Task<bool> SentSinceAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>Uses a link in ONE guarded statement - unused and unexpired, or nothing - and returns whose it was.</summary>
    Task<Guid?> TryClaimAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Marks the address confirmed in ONE guarded statement; false when it already was.</summary>
    Task<bool> TryConfirmAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);
}

public static class ConfirmationTokens
{
    /// <summary>A day: a confirmation grants nothing an attacker wants, and people open welcome emails late.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>At most one email a minute per account.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    public const string Invalid = "This link is invalid or has expired.";
}

/// <summary>
/// Stages a confirmation link and its email (specs/063): the token's hash and the <c>EmailConfirmation</c>
/// email are added to the context, so they commit with the caller's one save - a new account's, or a resend's.
/// Identity is the sender, so the token never crosses the broker, and the dispatcher scrubs it once sent.
/// </summary>
public class EmailConfirmations(IEmailConfirmationRepository tokens, IOutgoingEmailRepository emails, IAuditTrail audit)
{
    private readonly IEmailConfirmationRepository _tokens = tokens;
    private readonly IOutgoingEmailRepository _emails = emails;
    private readonly IAuditTrail _audit = audit;

    public async Task StageAsync(User user, string language, DateTime now, CancellationToken cancellationToken)
    {
        var token = ResetTokens.NewToken();

        await _tokens.AddAsync(new EmailConfirmationToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = ResetTokens.Hash(token),
            ExpiresAt = now + ConfirmationTokens.Lifetime,
            CreatedAt = now,
        }, cancellationToken);

        _emails.Stage(new OutgoingEmail
        {
            Id = Guid.CreateVersion7(),
            RecipientId = user.Id,
            Template = EmailTemplate.EmailConfirmation,
            DataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["token"] = token }),
            Language = string.IsNullOrWhiteSpace(language) ? EmailTemplates.DefaultLanguage : language,
            Status = OutgoingEmailStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        });

        await _audit.RecordAsync(AuditCategory.User, "EmailConfirmationSent", "User", user.Id.ToString(),
            $"A link to confirm {user.Email} was sent", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
    }
}

public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator() =>
        RuleFor(x => x.Token).NotEmpty().WithMessage(ConfirmationTokens.Invalid)
            .MaximumLength(200).WithMessage(ConfirmationTokens.Invalid);
}

public class EmailConfirmationHandlers(
    IUserRepository users,
    IEmailConfirmationRepository tokens,
    EmailConfirmations confirmations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditTrail audit) :
    IRequestHandler<ConfirmEmailCommand>,
    IRequestHandler<ResendConfirmationCommand>
{
    private readonly IUserRepository _users = users;
    private readonly IEmailConfirmationRepository _tokens = tokens;
    private readonly EmailConfirmations _confirmations = confirmations;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // One guarded statement decides: used, expired, replaced or never sent all end here, alike.
            var userId = await _tokens.TryClaimAsync(ResetTokens.Hash(request.Token), now, ct)
                ?? throw new ValidationException([new ValidationFailure(nameof(request.Token), ConfirmationTokens.Invalid)]);

            // Already confirmed (an older link, the address confirmed since): the link is spent, nothing changes.
            if (!await _tokens.TryConfirmAsync(userId, now, ct))
            {
                return;
            }

            var user = await _users.GetByIdAsync(userId, ct)
                ?? throw new ValidationException([new ValidationFailure(nameof(request.Token), ConfirmationTokens.Invalid)]);
            await _audit.RecordAsync(AuditCategory.User, "EmailConfirmed", "User", user.Id.ToString(),
                $"{user.Email} confirmed their email address", actor: AuditActors.Of(user), cancellationToken: ct);
            await _users.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task Handle(ResendConfirmationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var now = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("User not found.");
            if (user.EmailConfirmed)
            {
                throw new ConflictException("This email address is already confirmed.");
            }

            // One email a minute per account (the pattern from specs/062); the same 202 either way.
            if (await _tokens.SentSinceAsync(user.Id, now - ConfirmationTokens.MinimumInterval, ct))
            {
                return;
            }

            await _tokens.DeleteUnusedAsync(user.Id, ct);
            await _confirmations.StageAsync(user, request.Language, now, ct);
            await _users.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
