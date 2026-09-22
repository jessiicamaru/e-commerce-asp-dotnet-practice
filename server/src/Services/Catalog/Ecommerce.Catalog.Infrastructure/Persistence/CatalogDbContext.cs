using Ecommerce.Catalog.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantOption> VariantOptions => Set<VariantOption>();
    public DbSet<VariantPrice> VariantPrices => Set<VariantPrice>();
    public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
    public DbSet<VariantOptionTranslation> VariantOptionTranslations => Set<VariantOptionTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
