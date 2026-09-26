using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence.Repositories;

public class NotificationWordingStore(ActivityDbContext context) : INotificationWordingStore
{
    private readonly ActivityDbContext _context = context;

    public Task<NotificationWordingVersion?> CurrentAsync(string key, string language, CancellationToken cancellationToken = default) =>
        _context.NotificationWordingVersions.AsNoTracking()
            .Where(v => v.Key == key && v.Language == language)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<NotificationWordingVersion>> CurrentAllAsync(CancellationToken cancellationToken = default) =>
        _context.NotificationWordingVersions.AsNoTracking()
            .Where(v => v.Version == _context.NotificationWordingVersions
                .Where(o => o.Key == v.Key && o.Language == v.Language)
                .Max(o => o.Version))
            .ToListAsync(cancellationToken);

    public Task<List<NotificationWordingVersion>> HistoryAsync(string key, string language, CancellationToken cancellationToken = default) =>
        _context.NotificationWordingVersions.AsNoTracking()
            .Where(v => v.Key == key && v.Language == language)
            .OrderByDescending(v => v.Version)
            .ToListAsync(cancellationToken);

    public Task<NotificationWordingVersion?> GetVersionAsync(string key, string language, int version, CancellationToken cancellationToken = default) =>
        _context.NotificationWordingVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Key == key && v.Language == language && v.Version == version, cancellationToken);

    public Task<bool> TryAddAsync(NotificationWordingVersion version, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // The unique (key, language, version) decides between two saves at once.
            var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notification_wording_versions ("Id", "Key", "Language", "Version", "IsDefault", "Text", "CreatedAt", "CreatedBy")
                VALUES ({version.Id}, {version.Key}, {version.Language}, {version.Version}, {version.IsDefault}, {version.Text},
                        {version.CreatedAt}, {version.CreatedBy})
                ON CONFLICT ("Key", "Language", "Version") DO NOTHING
                """, cancellationToken);

            if (inserted == 1)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return inserted == 1;
        });
    }
}
