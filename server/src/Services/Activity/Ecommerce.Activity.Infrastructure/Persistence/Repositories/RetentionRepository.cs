using Ecommerce.Activity.Application.Retention;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence.Repositories;

/// <summary>
/// The batched deletes behind retention (specs/116 research D3): each a short statement over an index. Two instances at
/// once meet on the rows' locks, and the second finds them gone - it deletes nothing and fails nothing.
/// </summary>
public class RetentionRepository(ActivityDbContext context) : IRetentionRepository
{
    private readonly ActivityDbContext _context = context;

    public Task<int> DeleteReadNotificationsAsync(DateTime cutoff, int batch, CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications WHERE "Id" IN (
                SELECT "Id" FROM notifications WHERE "ReadAt" IS NOT NULL AND "ReadAt" < {cutoff} LIMIT {batch})
            """, cancellationToken);

    public async Task<int> TrimAuditEntriesAsync(
        DateTime cutoff, int batch, Func<int, CancellationToken, Task> stage, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();   // a retried attempt must not save the entry of the failed one
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var removed = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM audit_entries WHERE "Id" IN (
                    SELECT "Id" FROM audit_entries WHERE "OccurredAt" < {cutoff} LIMIT {batch})
                """, cancellationToken);
            if (removed > 0)
            {
                await stage(removed, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return removed;
        });
    }
}
