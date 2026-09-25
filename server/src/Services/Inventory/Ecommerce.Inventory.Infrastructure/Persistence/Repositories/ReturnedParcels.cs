using Ecommerce.Inventory.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence.Repositories;

public class ReturnedParcels(InventoryDbContext context) : IReturnedParcels
{
    private readonly InventoryDbContext _context = context;

    public async Task<bool> TryClaimAsync(Guid returnId, Guid orderId, DateTime at, CancellationToken cancellationToken = default) =>
        await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO returned_parcels ("ReturnId", "OrderId", "RestockedAt")
            VALUES ({returnId}, {orderId}, {at})
            ON CONFLICT ("ReturnId") DO NOTHING
            """, cancellationToken) == 1;
}
