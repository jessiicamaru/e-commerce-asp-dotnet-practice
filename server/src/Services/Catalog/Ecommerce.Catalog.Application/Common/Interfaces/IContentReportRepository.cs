using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>One thing reported, as the moderators' queue shows it (specs/101).</summary>
public record ReportedItem(
    ReportTarget TargetType,
    Guid TargetId,
    Guid ProductId,
    string ProductName,
    int ReportCount,
    Dictionary<string, int> Reasons,
    List<string> Details,
    DateTime FirstReportedAt,
    DateTime LastReportedAt);

public interface IContentReportRepository
{
    /// <summary>
    /// Inserts the report unless the person already has one open on the same thing - <c>ON CONFLICT DO NOTHING</c> on
    /// the partial unique index. False when there was one: two taps at once are one report.
    /// </summary>
    Task<bool> TryAddAsync(ContentReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes every open report of one thing in a single guarded <c>UPDATE ... WHERE "Status" = 'Open' RETURNING</c>
    /// and says who reported it - once each. Runs in the caller's transaction: called from a hide's stage callback, the
    /// reports close with the hide or not at all.
    /// </summary>
    Task<List<Guid>> CloseAsync(ReportTarget targetType, Guid targetId, ReportStatus outcome, Guid by, DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>What is open, per thing, most reported first then longest waiting.</summary>
    Task<(List<ReportedItem> Items, int TotalCount)> GetQueueAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction inside the execution strategy, joining the caller's when there is
    /// one, then saves what it staged.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}
