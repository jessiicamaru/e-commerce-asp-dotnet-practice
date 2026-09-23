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
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProductView> ProductViews => Set<ProductView>();
    public DbSet<ReviewEligibility> ReviewEligibility => Set<ReviewEligibility>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantOption> VariantOptions => Set<VariantOption>();
    public DbSet<VariantPrice> VariantPrices => Set<VariantPrice>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
    public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
    public DbSet<VariantOptionTranslation> VariantOptionTranslations => Set<VariantOptionTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
