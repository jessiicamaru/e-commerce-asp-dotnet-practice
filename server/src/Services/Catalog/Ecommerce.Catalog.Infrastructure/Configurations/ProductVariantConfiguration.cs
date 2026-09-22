using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants").HasKey(v => v.Id);

        // Application-generated, and for the backfilled variants it is the product's own id
        // (specs/020 research D2). Without this, EF takes a Guid key for database-generated and an
        // insert with the id already set is issued as an UPDATE that changes nothing.
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Sku).HasMaxLength(50).IsRequired();
        builder.HasIndex(v => v.Sku).IsUnique();

        builder.Property(v => v.Price).HasColumnType("decimal(18,2)");
        builder.Property(v => v.OptionSummary).HasMaxLength(200).IsRequired();
        builder.Property(v => v.IsActive).IsRequired().HasDefaultValue(true);

        // Same asymmetry as the product's flag: never announced must read as unavailable.
        builder.Property(v => v.Availability).IsRequired().HasDefaultValue(false);
        builder.Property(v => v.AvailabilityObservedAt);

        builder.Ignore(v => v.Sellable);

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Options)
            .WithOne()
            .HasForeignKey(o => o.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VariantOptionConfiguration : IEntityTypeConfiguration<VariantOption>
{
    public void Configure(EntityTypeBuilder<VariantOption> builder)
    {
        builder.ToTable("variant_options").HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(50).IsRequired();
        builder.Property(o => o.Value).HasMaxLength(100).IsRequired();

        // One Colour per variant. Which colours exist is not this schema's business (research D4).
        builder.HasIndex(o => new { o.VariantId, o.Name }).IsUnique();
    }
}
