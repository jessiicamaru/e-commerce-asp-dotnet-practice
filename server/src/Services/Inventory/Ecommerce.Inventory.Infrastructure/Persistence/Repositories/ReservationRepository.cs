using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence.Repositories;

public class ReservationRepository(InventoryDbContext context) : IReservationRepository
{
    private readonly InventoryDbContext _context = context;

    public async Task<List<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.StockReservations
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.ProductId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<StockReservation>> GetByOrderIdAndStatusAsync(
        Guid orderId,
        ReservationStatus status,
        CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: the settlement handlers mutate what comes back. Filtering on status
        // here is what makes a repeated settlement a no-op rather than a second stock movement.
        return await _context.StockReservations
            .Where(x => x.OrderId == orderId && x.Status == status)
            .OrderBy(x => x.ProductId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<StockReservation>> GetExpiredAsync(
        DateTime asOfUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.StockReservations
            .Where(x => x.Status == ReservationStatus.Held && x.ExpiresAt <= asOfUtc)
            .OrderBy(x => x.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.StockReservations.AnyAsync(x => x.OrderId == orderId, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<StockReservation> reservations, CancellationToken cancellationToken = default)
    {
        await _context.StockReservations.AddRangeAsync(reservations, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
