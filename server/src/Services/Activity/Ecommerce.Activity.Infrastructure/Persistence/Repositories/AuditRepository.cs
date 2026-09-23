using Ecommerce.Activity.Application.Audit;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence.Repositories;

public class AuditRepository(ActivityDbContext context) : IAuditRepository
{
    private readonly ActivityDbContext _context = context;

    public async Task<bool> TryAddAsync(AuditEntry e, CancellationToken cancellationToken = default)
    {
        // One statement, idempotent by the publisher's id: a redelivery affects no row (research D4).
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO audit_entries ("Id", "Category", "Action", "ActorId", "ActorEmail", "ActorRole",
                "SubjectType", "SubjectId", "Summary", "Before", "After", "Changes", "ChangeCount",
                "Service", "OccurredAt", "RecordedAt")
            VALUES ({e.Id}, {e.Category}, {e.Action}, {e.ActorId}, {e.ActorEmail}, {e.ActorRole},
                {e.SubjectType}, {e.SubjectId}, {e.Summary}, CAST({e.Before} AS jsonb), CAST({e.After} AS jsonb),
                CAST({e.Changes} AS jsonb), {e.ChangeCount}, {e.Service}, {e.OccurredAt}, {e.RecordedAt})
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);

        return inserted == 1;
    }

    public async Task<(List<AuditEntrySummaryResponse> Items, int TotalCount)> GetPageAsync(
        AuditFilter f, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditEntries.AsNoTracking().AsQueryable();

        if (f.Category is not null) query = query.Where(x => x.Category == f.Category);
        if (!string.IsNullOrWhiteSpace(f.Action)) query = query.Where(x => x.Action == f.Action);
        if (!string.IsNullOrWhiteSpace(f.Actor)) query = query.Where(x => x.ActorEmail != null && EF.Functions.ILike(x.ActorEmail, "%" + f.Actor.Trim() + "%"));
        if (f.ActorId is not null) query = query.Where(x => x.ActorId == f.ActorId);
        if (!string.IsNullOrWhiteSpace(f.SubjectType)) query = query.Where(x => x.SubjectType == f.SubjectType);
        if (!string.IsNullOrWhiteSpace(f.SubjectId)) query = query.Where(x => x.SubjectId == f.SubjectId);
        if (f.From is not null) query = query.Where(x => x.OccurredAt >= f.From);
        if (f.To is not null) query = query.Where(x => x.OccurredAt <= f.To);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditEntrySummaryResponse(
                x.Id, x.Category, x.Action, x.ActorId, x.ActorEmail, x.ActorRole, x.SubjectType, x.SubjectId,
                x.Summary, x.Service, x.OccurredAt, x.ChangeCount))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<AuditEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.AuditEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<List<CategoryCountResponse>> CountByCategoryAsync(
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditEntries.AsNoTracking().AsQueryable();
        if (from is not null) query = query.Where(x => x.OccurredAt >= from);
        if (to is not null) query = query.Where(x => x.OccurredAt <= to);

        return await query
            .GroupBy(x => x.Category)
            .Select(g => new CategoryCountResponse(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
    }
}
