using Ecommerce.Activity.Application.Audit;
using Ecommerce.Activity.Domain.Entities;

namespace Ecommerce.Activity.Application.Common.Interfaces;

public interface IAuditRepository
{
    /// <summary>
    /// Inserts the entry unless one with its id exists - <c>ON CONFLICT DO NOTHING</c> - and says whether it
    /// did. A redelivered message is therefore recorded once (research D4).
    /// </summary>
    Task<bool> TryAddAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    Task<(List<AuditEntrySummaryResponse> Items, int TotalCount)> GetPageAsync(
        AuditFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AuditEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<CategoryCountResponse>> CountByCategoryAsync(
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
