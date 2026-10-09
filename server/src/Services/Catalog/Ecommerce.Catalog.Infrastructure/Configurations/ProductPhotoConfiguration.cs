using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

/// <summary>A product's photographs after its cover (specs/160, #368): one table, added by an expand-only migration.</summary>
public class ProductPhotoConfiguration : IEntityTypeConfiguration<ProductPhoto>
{
    public void Configure(EntityTypeBuilder<ProductPhoto> builder)
    {
        builder.ToTable("product_photos").HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.ContentType).HasMaxLength(32).IsRequired();
        builder.Property(p => p.StorageKey).HasMaxLength(128).IsRequired();

        builder.HasIndex(p => new { p.ProductId, p.Position });
        builder.HasIndex(p => p.StorageKey).IsUnique();

        // The rows go with their product; the files are deleted by the handler after the commit (specs/029).
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
