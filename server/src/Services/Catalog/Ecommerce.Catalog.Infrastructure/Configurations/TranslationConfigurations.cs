using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ProductTranslationConfiguration : IEntityTypeConfiguration<ProductTranslation>
{
    public void Configure(EntityTypeBuilder<ProductTranslation> builder)
    {
        builder.ToTable("product_translations").HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Language).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);

        // One text per language per product. Two Vietnamese names would make the answer depend on
        // which row the query happened to read first.
        builder.HasIndex(t => new { t.ProductId, t.Language }).IsUnique();

        builder.HasOne<Product>()
            .WithMany(p => p.Translations)
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VariantOptionTranslationConfiguration : IEntityTypeConfiguration<VariantOptionTranslation>
{
    public void Configure(EntityTypeBuilder<VariantOptionTranslation> builder)
    {
        builder.ToTable("variant_option_translations").HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Language).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Value).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => new { t.OptionId, t.Language }).IsUnique();

        builder.HasOne<VariantOption>()
            .WithMany(o => o.Translations)
            .HasForeignKey(t => t.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
