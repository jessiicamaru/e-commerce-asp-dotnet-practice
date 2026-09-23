using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Payment.Infrastructure.Persistence;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Entities.Payment> Payments => Set<Domain.Entities.Payment>();
    public DbSet<Domain.Entities.Refund> Refunds => Set<Domain.Entities.Refund>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
