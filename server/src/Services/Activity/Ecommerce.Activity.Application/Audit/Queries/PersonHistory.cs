using System.Text.Json;
using Ecommerce.Activity.Application.Common;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using FluentValidation;
using MediatR;

namespace Ecommerce.Activity.Application.Audit.Queries;

/// <summary>
/// What staff decided about one person before (#198, specs/100): locks, bans, shop decisions, reviews and questions
/// hidden, products taken down. <b>Moderation only</b> - the category is fixed here, never taken from the request, so
/// this route cannot read a person's Security or Order entries.
/// </summary>
public record GetPersonModerationHistoryQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResponse<ModerationHistoryEntryResponse>>;

/// <summary>
/// One decision - who, what, when and why. No snapshots and no diff: a moderator reads the history to decide, not to
/// audit the fields.
/// </summary>
/// <param name="Reason">The reason given with the decision, read from the entry's "after" snapshot; null when none.</param>
public record ModerationHistoryEntryResponse(
    Guid Id,
    string Action,
    string? ActorEmail,
    string? ActorRole,
    string SubjectType,
    string? SubjectId,
    string Summary,
    string? Reason,
    DateTime OccurredAt);

public class GetPersonModerationHistoryQueryValidator : AbstractValidator<GetPersonModerationHistoryQuery>
{
    public GetPersonModerationHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetPersonModerationHistoryQueryHandler(IAuditRepository audit)
    : IRequestHandler<GetPersonModerationHistoryQuery, PagedResponse<ModerationHistoryEntryResponse>>
{
    private readonly IAuditRepository _audit = audit;

    public async Task<PagedResponse<ModerationHistoryEntryResponse>> Handle(
        GetPersonModerationHistoryQuery q, CancellationToken cancellationToken)
    {
        var (items, total) = await _audit.GetAboutAsync(q.UserId, AuditCategory.Moderation, q.Page, q.PageSize, cancellationToken);
        return new PagedResponse<ModerationHistoryEntryResponse>(
            items.Select(e => new ModerationHistoryEntryResponse(
                e.Id, e.Action, e.ActorEmail, e.ActorRole, e.SubjectType, e.SubjectId, e.Summary, ReasonIn(e.After), e.OccurredAt))
                .ToList(),
            q.Page, q.PageSize, total);
    }

    /// <summary>
    /// The first top-level string property whose name ends in "reason" - <c>reason</c> on a hidden review or a rejected
    /// shop, <c>lockReason</c> / <c>banReason</c> on an account. Snapshots are written by the publishers (camelCase).
    /// </summary>
    public static string? ReasonIn(string? after)
    {
        if (after is null) return null;
        using var doc = JsonDocument.Parse(after);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (p.Name.EndsWith("reason", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                return p.Value.GetString();
        }

        return null;
    }
}
