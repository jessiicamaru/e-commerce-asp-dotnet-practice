using Ecommerce.Cart.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence.Repositories;

/// <summary>What Cart erases when an account is deleted (specs/112): everything it holds about the person.</summary>
public class AccountErasure(CartDbContext context) : IAccountErasure
{
    private readonly CartDbContext _context = context;

    public async Task EraseAsync(Guid personId, string email, CancellationToken cancellationToken = default)
    {
        // Inside a consumer the outbox may hold a transaction already: take part in it.
        if (_context.Database.CurrentTransaction is not null)
        {
            await EraseInTransactionAsync(personId, email, cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await EraseInTransactionAsync(personId, email, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task EraseInTransactionAsync(Guid personId, string email, CancellationToken cancellationToken)
    {
        // The lines go by the foreign key's cascade.
        await _context.Carts.Where(c => c.UserId == personId).ExecuteDeleteAsync(cancellationToken);
        await _context.CheckoutOutcomes.Where(o => o.UserId == personId).ExecuteDeleteAsync(cancellationToken);
    }
}
