using Ecommerce.Activity.Domain.Entities;

namespace Ecommerce.Activity.Application.Common.Interfaces;

/// <summary>The saved versions of the notifications' words (specs/078).</summary>
public interface INotificationWordingStore
{
    Task<NotificationWordingVersion?> CurrentAsync(string key, string language, CancellationToken cancellationToken = default);

    /// <summary>The newest version of every key and language that has one.</summary>
    Task<List<NotificationWordingVersion>> CurrentAllAsync(CancellationToken cancellationToken = default);

    Task<List<NotificationWordingVersion>> HistoryAsync(string key, string language, CancellationToken cancellationToken = default);

    Task<NotificationWordingVersion?> GetVersionAsync(string key, string language, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a version unless its number is taken (<c>INSERT ... ON CONFLICT DO NOTHING</c>), and only if it inserted,
    /// <paramref name="stage"/> (the audit entry) and a save - one transaction. False: somebody saved that number first.
    /// </summary>
    Task<bool> TryAddAsync(NotificationWordingVersion version, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);
}

/// <summary>What a notice's words may contain (specs/078): emphasis and links - nothing a bell cannot show in a line.</summary>
public interface INoticeSanitizer
{
    string Sanitize(string html);
}
