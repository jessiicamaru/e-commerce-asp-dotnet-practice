using Ecommerce.Activity.Application.Notifications;
using Ecommerce.Activity.Domain.Entities;

namespace Ecommerce.Activity.Application.Common.Interfaces;

public interface INotificationRepository
{
    /// <summary>Inserts unless one with its id exists (<c>ON CONFLICT DO NOTHING</c>); says whether it did.</summary>
    Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>The recipient's notifications, newest first. The recipient is part of the query.</summary>
    Task<(List<NotificationResponse> Items, int TotalCount)> GetPageAsync(
        Guid recipientId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one read, if it is the recipient's - one guarded statement. Null when there is no such
    /// notification of theirs; false when it already was read.
    /// </summary>
    Task<bool?> TryMarkReadAsync(Guid id, Guid recipientId, DateTime at, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(Guid recipientId, DateTime at, CancellationToken cancellationToken = default);
}
