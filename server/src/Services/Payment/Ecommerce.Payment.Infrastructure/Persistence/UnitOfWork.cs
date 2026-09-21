using Ecommerce.Payment.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Payment.Infrastructure.Persistence;

public class UnitOfWork(PaymentDbContext context) : IUnitOfWork
{
    private readonly PaymentDbContext _context = context;

    public void DiscardPendingChanges() => _context.ChangeTracker.Clear();

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
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
