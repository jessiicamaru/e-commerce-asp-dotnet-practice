using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class ShopApplicationRepository(ApplicationDbContext context) : IShopApplicationRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task AddAsync(ShopApplication application, CancellationToken cancellationToken = default) =>
        await _context.ShopApplications.AddAsync(application, cancellationToken);

    public Task<bool> HasPendingAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ShopApplications.AnyAsync(a => a.UserId == userId && a.Status == ShopApplicationStatus.Pending, cancellationToken);

    public Task<List<ShopApplication>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ShopApplications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ShopApplicationRow?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithApplicant(_context.ShopApplications.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(List<ShopApplicationRow> Items, int TotalCount)> GetPageAsync(
        ShopApplicationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ShopApplications.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);

        // Pending is a queue - oldest first, so nobody waits behind people who applied after them.
        // Decided ones are history - newest first.
        // Ordered AFTER the join: an order inside a subquery that is then joined is not kept.
        var joined = from a in query
                     join u in _context.Users.AsNoTracking() on a.UserId equals u.Id
                     select new { a, u.Email, u.FirstName, u.LastName, u.EmailConfirmedAt };
        var ordered = status == ShopApplicationStatus.Pending
            ? joined.OrderBy(x => x.a.CreatedAt).ThenBy(x => x.a.Id)
            : joined.OrderByDescending(x => x.a.CreatedAt).ThenBy(x => x.a.Id);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ShopApplicationRow(x.a, x.Email, x.FirstName, x.LastName, x.EmailConfirmedAt != null))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<bool> TryDecideAsync(
        Guid id,
        ShopApplicationStatus decision,
        string? reason,
        Guid decidedBy,
        DateTime decidedAt,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var decided = await _context.ShopApplications
                .Where(a => a.Id == id && a.Status == ShopApplicationStatus.Pending)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(a => a.Status, decision)
                    .SetProperty(a => a.DecisionReason, reason)
                    .SetProperty(a => a.DecidedBy, decidedBy)
                    .SetProperty(a => a.DecidedAt, decidedAt), cancellationToken);

            if (decided == 0)
            {
                return false;   // rolled back on dispose; somebody else decided first, or it was never pending
            }

            await stage(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);

    /// <summary>Filtered and ordered first, projected last - EF cannot translate a filter on a record it built.</summary>
    private IQueryable<ShopApplicationRow> WithApplicant(IQueryable<ShopApplication> applications) =>
        from a in applications
        join u in _context.Users.AsNoTracking() on a.UserId equals u.Id
        select new ShopApplicationRow(a, u.Email, u.FirstName, u.LastName, u.EmailConfirmedAt != null);
}
