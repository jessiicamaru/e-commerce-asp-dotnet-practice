using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence.Repositories;

public class CheckoutOutcomeRepository(CartDbContext context) : ICheckoutOutcomeRepository
{
    private readonly CartDbContext _context = context;

    public async Task<CheckoutOutcome> GetOrCreateForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        // Whichever of the three order events arrives first creates the row. The others find it.
        await _context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO checkout_outcomes ("OrderId", "Outcome", "Applied", "UpdatedAt")
            VALUES ({orderId}, 'Pending', false, {DateTime.UtcNow})
            ON CONFLICT ("OrderId") DO NOTHING
            """,
            cancellationToken);

        // Every event for this order takes the same lock before acting. Two arriving at once are
        // serialised, so "whichever is second applies" cannot become "both apply" or "neither does".
        await _context.Database.ExecuteSqlAsync(
            $"""SELECT 1 FROM checkout_outcomes WHERE "OrderId" = {orderId} FOR UPDATE""",
            cancellationToken);

        return await _context.CheckoutOutcomes.SingleAsync(o => o.OrderId == orderId, cancellationToken);
    }
}
