using Ecommerce.Activity.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence;

public class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ActivityDbContext).Assembly);

        // The inbox that deduplicates deliveries, as in every other service.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
