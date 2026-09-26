using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Application.Email;

/// <summary>
/// One email as an administrator sees it (specs/087, #175): who it was for, what and in which language, and how
/// delivery went. <b>Never its data</b> - a reset or confirmation email's data is a token (specs/061, 063).
/// </summary>
/// <param name="CanRetry">A failed email an administrator may send again - never a reset or confirmation link.</param>
public record OutgoingEmailResponse(
    Guid Id,
    Guid RecipientId,
    string? RecipientEmail,
    string Template,
    string Language,
    string Status,
    int Attempts,
    string? LastError,
    DateTime CreatedAt,
    DateTime NextAttemptAt,
    DateTime? SentAt,
    bool CanRetry)
{
    public static OutgoingEmailResponse From(OutgoingEmail e, string? recipientEmail) => new(
        e.Id, e.RecipientId, recipientEmail, e.Template, e.Language, e.Status.ToString(), e.Attempts, e.LastError,
        e.CreatedAt, e.NextAttemptAt, e.SentAt, EmailDelivery.MayRetry(e));
}

public record OutgoingEmailPage(List<OutgoingEmailResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>The emails in one state, newest first, optionally for recipients whose address contains <paramref name="Search"/>.</summary>
public record GetOutgoingEmailsQuery(string Status = "Failed", string? Search = null, int Page = 1, int PageSize = 12) : IRequest<OutgoingEmailPage>;

/// <summary>Puts a failed email back in the queue: the dispatcher tries it again at once, from the first attempt.</summary>
public record RetryEmailCommand(Guid Id) : IRequest<OutgoingEmailResponse>;

public class GetOutgoingEmailsQueryValidator : AbstractValidator<GetOutgoingEmailsQuery>
{
    public GetOutgoingEmailsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => Enum.TryParse<OutgoingEmailStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status is Pending, Sent or Failed.");
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Search).MaximumLength(255);
    }
}

public static class EmailDelivery
{
    public const string NotFound = "Email not found.";

    /// <summary>
    /// Only a failed email, and never one carrying a link that expires (specs/061, 063): a reset link is dead after 30
    /// minutes and sending it later helps nobody - the person asks for a new one.
    /// </summary>
    public static bool MayRetry(OutgoingEmail email) =>
        email.Status == OutgoingEmailStatus.Failed && !EmailTemplates.ScrubbedOnceSent.Contains(email.Template);
}

public class EmailDeliveryHandlers(IOutgoingEmailRepository emails, IUnitOfWork unitOfWork, IAuditTrail audit) :
    IRequestHandler<GetOutgoingEmailsQuery, OutgoingEmailPage>,
    IRequestHandler<RetryEmailCommand, OutgoingEmailResponse>
{
    private readonly IOutgoingEmailRepository _emails = emails;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAuditTrail _audit = audit;

    public async Task<OutgoingEmailPage> Handle(GetOutgoingEmailsQuery request, CancellationToken cancellationToken)
    {
        var status = Enum.Parse<OutgoingEmailStatus>(request.Status, ignoreCase: true);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var (items, total) = await _emails.PageAsync(status, search, request.Page, request.PageSize, cancellationToken);
        return new OutgoingEmailPage(
            items.Select(x => OutgoingEmailResponse.From(x.Email, x.RecipientEmail)).ToList(), request.Page, request.PageSize, total);
    }

    public async Task<OutgoingEmailResponse> Handle(RetryEmailCommand request, CancellationToken cancellationToken)
    {
        var email = await _emails.GetAsync(request.Id, cancellationToken) ?? throw new NotFoundException(EmailDelivery.NotFound);
        if (EmailTemplates.ScrubbedOnceSent.Contains(email.Template))
            throw new ConflictException("A reset or confirmation link expires; the person asks for a new one instead.");

        var now = DateTime.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Guarded: of two administrators at once, or a retry of an email already back in the queue, one moves it.
            if (!await _emails.TryRetryAsync(email.Id, now, ct))
                throw new ConflictException($"This email is {email.Status.ToString().ToLowerInvariant()}; only a failed one can be sent again.");

            await _audit.RecordAsync(AuditCategory.System, "EmailRetried", "Email", email.Id.ToString(),
                $"A failed {email.Template} email was put back in the queue",
                new { Status = "Failed", email.Attempts, email.LastError }, new { Status = "Pending", Attempts = 0 },
                cancellationToken: ct);
            await _emails.SaveChangesAsync(ct);
        }, cancellationToken);

        var (retried, recipient) = await _emails.GetWithRecipientAsync(email.Id, cancellationToken)
            ?? throw new NotFoundException(EmailDelivery.NotFound);
        return OutgoingEmailResponse.From(retried, recipient);
    }
}
