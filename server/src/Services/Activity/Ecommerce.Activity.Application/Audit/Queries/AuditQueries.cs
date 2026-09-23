using System.Text.Json;
using Ecommerce.Activity.Application.Common;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Activity.Application.Audit.Queries;

/// <summary>A page of the log, newest first, filtered (specs/041 US1).</summary>
public record GetAuditEntriesQuery(
    string? Category = null,
    string? Action = null,
    string? Actor = null,
    Guid? ActorId = null,
    string? SubjectType = null,
    string? SubjectId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResponse<AuditEntrySummaryResponse>>;

public class GetAuditEntriesQueryValidator : AbstractValidator<GetAuditEntriesQuery>
{
    public GetAuditEntriesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Category)
            .Must(c => c is null || AuditCategory.All.Contains(c))
            .WithMessage("Category must be one of: " + string.Join(", ", AuditCategory.All) + ".");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("From must not be after To.");
    }
}

public class GetAuditEntriesQueryHandler(IAuditRepository audit)
    : IRequestHandler<GetAuditEntriesQuery, PagedResponse<AuditEntrySummaryResponse>>
{
    private readonly IAuditRepository _audit = audit;

    public async Task<PagedResponse<AuditEntrySummaryResponse>> Handle(GetAuditEntriesQuery q, CancellationToken cancellationToken)
    {
        var filter = new AuditFilter(q.Category, q.Action, q.Actor, q.ActorId, q.SubjectType, q.SubjectId, q.From, q.To);
        var (items, total) = await _audit.GetPageAsync(filter, q.Page, q.PageSize, cancellationToken);
        return new PagedResponse<AuditEntrySummaryResponse>(items, q.Page, q.PageSize, total);
    }
}

/// <summary>One entry in full, with its diff.</summary>
public record GetAuditEntryQuery(Guid Id) : IRequest<AuditEntryResponse>;

public class GetAuditEntryQueryHandler(IAuditRepository audit) : IRequestHandler<GetAuditEntryQuery, AuditEntryResponse>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAuditRepository _audit = audit;

    public async Task<AuditEntryResponse> Handle(GetAuditEntryQuery request, CancellationToken cancellationToken)
    {
        var e = await _audit.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Audit entry not found.");

        return new AuditEntryResponse(
            e.Id, e.Category, e.Action, e.ActorId, e.ActorEmail, e.ActorRole, e.SubjectType, e.SubjectId,
            e.Summary, e.Service, e.OccurredAt, e.RecordedAt,
            Parse(e.Before), Parse(e.After),
            JsonSerializer.Deserialize<List<AuditChange>>(e.Changes, Json) ?? []);
    }

    private static JsonElement? Parse(string? json) =>
        json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}

/// <summary>How many entries each category holds in a period - the tabs' numbers.</summary>
public record GetAuditSummaryQuery(DateTime? From = null, DateTime? To = null) : IRequest<List<CategoryCountResponse>>;

public class GetAuditSummaryQueryHandler(IAuditRepository audit) : IRequestHandler<GetAuditSummaryQuery, List<CategoryCountResponse>>
{
    private readonly IAuditRepository _audit = audit;

    public Task<List<CategoryCountResponse>> Handle(GetAuditSummaryQuery request, CancellationToken cancellationToken) =>
        _audit.CountByCategoryAsync(request.From, request.To, cancellationToken);
}
