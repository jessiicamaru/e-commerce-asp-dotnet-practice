using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

/// <summary>An application with who made it, for staff (specs/044).</summary>
public record ShopApplicationRow(ShopApplication Application, string Email, string FirstName, string LastName);

public interface IShopApplicationRepository
{
    Task AddAsync(ShopApplication application, CancellationToken cancellationToken = default);

    Task<bool> HasPendingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<ShopApplication>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ShopApplicationRow?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(List<ShopApplicationRow> Items, int TotalCount)> GetPageAsync(
        ShopApplicationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides a PENDING application, in one transaction with whatever <paramref name="stage"/> writes: a
    /// guarded <c>UPDATE ... WHERE "Status" = 'Pending'</c> decides who wins when two staff decide at once,
    /// and only the winner's stage runs. Returns whether this call decided it.
    /// </summary>
    Task<bool> TryDecideAsync(
        Guid id,
        ShopApplicationStatus decision,
        string? reason,
        Guid decidedBy,
        DateTime decidedAt,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
