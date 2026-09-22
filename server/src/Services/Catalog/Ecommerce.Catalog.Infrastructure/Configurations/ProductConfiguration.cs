using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", t =>
        {
            // An image is a type AND a version, or neither (specs/019). Half of one would give a
            // product an address that serves nothing.
            t.HasCheckConstraint("CK_products_image_complete",
                "(\"ImageContentType\" IS NULL) = (\"ImageUpdatedAt\" IS NULL)");

            // Only what ImageFormat recognises by its bytes - never anything a client merely claimed.
            t.HasCheckConstraint("CK_products_image_type",
                "\"ImageContentType\" IS NULL OR \"ImageContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");
        }).HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");

        // Required, defaulting to false: a product Inventory has never announced must read as
        // unavailable rather than as buyable (FR-005). The default also covers every row the
        // migration carries over, since Catalog cannot ask Inventory about them.
        builder.Property(p => p.Availability)
            .IsRequired()
            .HasDefaultValue(false);

        // Nullable on purpose - "never told" has to be distinguishable from "told a long time ago",
        // because the consumer's guard compares against it.
        builder.Property(p => p.AvailabilityObservedAt);

        // Nullable, no default: existing rows read as "no image", and an earlier image never selects
        // these columns - additive, as the constitution requires.
        builder.Property(p => p.ImageContentType).HasMaxLength(20);
        builder.Property(p => p.ImageUpdatedAt);

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
