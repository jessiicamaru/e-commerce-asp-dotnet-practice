using System.Text.Json;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Application.Notifications;
using Ecommerce.Activity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence.Repositories;

public class NotificationRepository(ActivityDbContext context) : INotificationRepository
{
    private readonly ActivityDbContext _context = context;

    public async Task<bool> TryAddAsync(Notification n, CancellationToken cancellationToken = default)
    {
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notifications ("Id", "RecipientId", "Kind", "Data", "Link", "CreatedAt", "ReadAt")
            VALUES ({n.Id}, {n.RecipientId}, {n.Kind}, CAST({n.Data} AS jsonb), {n.Link}, {n.CreatedAt}, NULL)
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);
        return inserted == 1;
    }

    public async Task<(List<NotificationResponse> Items, int TotalCount)> GetPageAsync(
        Guid recipientId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications.AsNoTracking().Where(x => x.RecipientId == recipientId);
        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (rows.Select(x => new NotificationResponse(
            x.Id, x.Kind, JsonSerializer.Deserialize<Dictionary<string, string>>(x.Data) ?? [], x.Link, x.CreatedAt, x.ReadAt))
            .ToList(), total);
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        _context.Notifications.CountAsync(x => x.RecipientId == recipientId && x.ReadAt == null, cancellationToken);

    public async Task<bool?> TryMarkReadAsync(Guid id, Guid recipientId, DateTime at, CancellationToken cancellationToken = default)
    {
        // The recipient is in the statement: someone else's notification is never touched, only not found.
        var marked = await _context.Notifications
            .Where(x => x.Id == id && x.RecipientId == recipientId && x.ReadAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.ReadAt, at), cancellationToken);

        if (marked == 1)
        {
            return true;
        }

        var theirs = await _context.Notifications.AnyAsync(x => x.Id == id && x.RecipientId == recipientId, cancellationToken);
        return theirs ? false : null;
    }

    public Task<int> MarkAllReadAsync(Guid recipientId, DateTime at, CancellationToken cancellationToken = default) =>
        _context.Notifications
            .Where(x => x.RecipientId == recipientId && x.ReadAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.ReadAt, at), cancellationToken);
}
