using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class ContentReportRepository(CatalogDbContext context) : IContentReportRepository
{
    private readonly CatalogDbContext _context = context;

    public async Task<bool> TryAddAsync(ContentReport r, CancellationToken cancellationToken = default) =>
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content_reports ("Id", "TargetType", "TargetId", "ProductId", "ReporterId", "Reason", "Details", "Status", "CreatedAt")
            VALUES ({r.Id}, {r.TargetType.ToString()}, {r.TargetId}, {r.ProductId}, {r.ReporterId}, {r.Reason.ToString()},
                    {r.Details}, 'Open', {r.CreatedAt})
            ON CONFLICT ("TargetType", "TargetId", "ReporterId") WHERE "Status" = 'Open' DO NOTHING
            """, cancellationToken) == 1;

    public async Task<List<Guid>> CloseAsync(ReportTarget targetType, Guid targetId, ReportStatus outcome, Guid by, DateTime now,
        CancellationToken cancellationToken = default) =>
        // DISTINCT is not needed: one open report per reporter is what the unique index says.
        await _context.Database.SqlQuery<Guid>($"""
            UPDATE content_reports SET "Status" = {outcome.ToString()}, "ResolvedAt" = {now}, "ResolvedBy" = {by}
             WHERE "TargetType" = {targetType.ToString()} AND "TargetId" = {targetId} AND "Status" = 'Open'
            RETURNING "ReporterId" AS "Value"
            """).ToListAsync(cancellationToken);

    public async Task<(List<ReportedItem> Items, int TotalCount)> GetQueueAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var open = _context.ContentReports.AsNoTracking().Where(r => r.Status == ReportStatus.Open);

        var groups = open.GroupBy(r => new { r.TargetType, r.TargetId, r.ProductId })
            .Select(g => new
            {
                g.Key.TargetType,
                g.Key.TargetId,
                g.Key.ProductId,
                Count = g.Count(),
                First = g.Min(r => r.CreatedAt),
                Last = g.Max(r => r.CreatedAt),
            });

        var total = await groups.CountAsync(cancellationToken);
        var rows = await groups
            .OrderByDescending(g => g.Count).ThenBy(g => g.First).ThenBy(g => g.TargetId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = rows.Select(g => g.TargetId).ToList();
        var reports = await open.Where(r => ids.Contains(r.TargetId))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.TargetType, r.TargetId, r.Reason, r.Details })
            .ToListAsync(cancellationToken);
        var productIds = rows.Select(g => g.ProductId).Distinct().ToList();
        var names = await _context.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var items = rows.Select(g =>
        {
            var mine = reports.Where(r => r.TargetType == g.TargetType && r.TargetId == g.TargetId).ToList();
            return new ReportedItem(
                g.TargetType, g.TargetId, g.ProductId, names.GetValueOrDefault(g.ProductId, ""), g.Count,
                mine.GroupBy(r => r.Reason.ToString()).ToDictionary(x => x.Key, x => x.Count()),
                // The latest few in the reporters' own words: enough to judge, not a wall of text.
                mine.Where(r => !string.IsNullOrWhiteSpace(r.Details)).Select(r => r.Details!).Take(3).ToList(),
                g.First, g.Last);
        }).ToList();

        return (items, total);
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        async Task<T> RunAsync(CancellationToken ct)
        {
            var result = await work(ct);
            await _context.SaveChangesAsync(ct);
            return result;
        }

        if (_context.Database.CurrentTransaction is not null)
        {
            return await RunAsync(cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var result = await RunAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
