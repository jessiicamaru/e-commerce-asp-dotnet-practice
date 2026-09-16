using Ecommerce.Inventory.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence;

public class UnitOfWork(InventoryDbContext context) : IUnitOfWork
{
    private readonly InventoryDbContext _context = context;

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        // Nested call: already inside a transaction, so just take part in it. Opening a second one
        // would silently detach the row locks the outer transaction is holding.
        if (_context.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await operation(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }
}
