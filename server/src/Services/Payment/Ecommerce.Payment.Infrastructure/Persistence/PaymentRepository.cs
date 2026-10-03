using Ecommerce.Payment.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Payment.Infrastructure.Persistence;

public class PaymentRepository(PaymentDbContext context) : IPaymentRepository
{
    private readonly PaymentDbContext _context = context;

    public async Task<Domain.Entities.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    }

    public async Task<(List<Domain.Entities.Payment> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        Guid? orderId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Payments.AsNoTracking().AsQueryable();

        if (orderId.HasValue)
        {
            query = query.Where(x => x.OrderId == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status.ToString() == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ProcessedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Domain.Entities.Payment payment, CancellationToken cancellationToken = default)
    {
        await _context.Payments.AddAsync(payment, cancellationToken);
    }

    public async Task<Domain.Entities.Refund?> GetRefundAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId && x.ReturnId == null && x.PartId == null, cancellationToken);
    }

    public async Task<Dictionary<Guid, Domain.Entities.Refund>> GetRefundsAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .AsNoTracking()
            // The whole order's refund only: a part's (specs/104) or a return's would be a second row per order.
            .Where(x => orderIds.Contains(x.OrderId) && x.ReturnId == null && x.PartId == null)
            .ToDictionaryAsync(x => x.OrderId, cancellationToken);
    }

    public async Task AddRefundAsync(Domain.Entities.Refund refund, CancellationToken cancellationToken = default)
    {
        await _context.Refunds.AddAsync(refund, cancellationToken);
    }

    public Task<Domain.Entities.Refund?> GetReturnRefundAsync(Guid returnId, CancellationToken cancellationToken = default) =>
        _context.Refunds.AsNoTracking().FirstOrDefaultAsync(x => x.ReturnId == returnId, cancellationToken);

    public Task<Domain.Entities.Refund?> GetPartRefundAsync(Guid partId, CancellationToken cancellationToken = default) =>
        _context.Refunds.AsNoTracking().FirstOrDefaultAsync(x => x.PartId == partId, cancellationToken);

    public async Task<decimal> GetRefundedTotalAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await _context.Refunds.AsNoTracking().Where(x => x.OrderId == orderId).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public Task<Domain.Entities.PaymentCheckout?> GetCheckoutByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _context.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    public Task<Domain.Entities.PaymentCheckout?> GetCheckoutByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        _context.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == reference, cancellationToken);

    public async Task AddCheckoutAsync(Domain.Entities.PaymentCheckout checkout, CancellationToken cancellationToken = default)
    {
        await _context.Checkouts.AddAsync(checkout, cancellationToken);
    }

    public async Task<bool> ClaimCheckoutAsync(
        Guid checkoutId, string responseCode, string? providerReference, DateTime at, CancellationToken cancellationToken = default)
    {
        // Guarded: of every copy of one notification, exactly one finds the checkout still waiting.
        var changed = await _context.Checkouts
            .Where(x => x.Id == checkoutId && x.CompletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.CompletedAt, at)
                .SetProperty(x => x.ResponseCode, responseCode)
                .SetProperty(x => x.ProviderReference, providerReference), cancellationToken);
        return changed == 1;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
