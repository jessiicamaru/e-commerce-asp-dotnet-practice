using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence.Repositories;

public class CartRepository(CartDbContext context) : ICartRepository
{
    private readonly CartDbContext _context = context;

    public Task<Domain.Entities.Cart?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => _context.Carts
            .AsNoTracking()
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task<Domain.Entities.Cart> GetOrCreateForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Does nothing if the customer already has a cart - and does nothing if another request is
        // creating one at this very moment. The unique index on UserId decides, not this code.
        await _context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO carts ("Id", "UserId", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.CreateVersion7()}, {userId}, {now}, {now})
            ON CONFLICT ("UserId") DO NOTHING
            """,
            cancellationToken);

        return await GetForUpdateAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"Cart for {userId} vanished after being created.");
    }

    public async Task<Domain.Entities.Cart?> GetForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Take the row lock first, on its own. SELECT ... FOR UPDATE cannot be combined with an
        // Include - EF wraps the raw SQL in a subquery and the lock clause is lost - so the lock is
        // taken here and the cart and its lines are loaded normally afterwards, under that lock.
        await _context.Database.ExecuteSqlAsync(
            $"""SELECT 1 FROM carts WHERE "UserId" = {userId} FOR UPDATE""",
            cancellationToken);

        return await _context.Carts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public void RemoveLine(CartLine line) => _context.CartLines.Remove(line);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
