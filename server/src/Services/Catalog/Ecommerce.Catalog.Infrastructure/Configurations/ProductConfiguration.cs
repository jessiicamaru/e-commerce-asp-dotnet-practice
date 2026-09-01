using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // TODO: Configure Product entity for EF Core Fluent API:
        // - Set table name to "products"
        // - Set primary key
        // - Name: MaxLength(200), IsRequired
        // - Description: MaxLength(2000)
        // - Sku: MaxLength(50), IsRequired, Unique Index
        // - Price: HasColumnType("decimal(18,2)")
        // - Relationship: Foreign key CategoryId pointing to Category

        builder.ToTable("products").HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
