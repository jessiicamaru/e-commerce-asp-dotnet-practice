using Ecommerce.Inventory.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    public DbSet<ReturnedParcel> ReturnedParcels => Set<ReturnedParcel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

        // Outbox for the replies this service publishes, inbox for the duplicate deliveries it
        // receives. Both live in this database so a message and its stock change share one commit.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
