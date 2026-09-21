using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence;

public class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Entities.Cart> Carts => Set<Domain.Entities.Cart>();
    public DbSet<Domain.Entities.CartLine> CartLines => Set<Domain.Entities.CartLine>();
    public DbSet<Domain.Entities.CheckoutOutcome> CheckoutOutcomes => Set<Domain.Entities.CheckoutOutcome>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartDbContext).Assembly);
    }
}
