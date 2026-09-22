using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class VariantPriceConfiguration : IEntityTypeConfiguration<VariantPrice>
{
    public void Configure(EntityTypeBuilder<VariantPrice> builder)
    {
        builder.ToTable("variant_prices", t =>
            // A negative price is not a discount, it is a shop that pays people to take cameras.
            t.HasCheckConstraint("CK_variant_prices_amount", "\"Amount\" >= 0"))
            .HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();

        // One price per currency per variant. Two dollar prices would make what a customer is
        // charged depend on which row the query read first - the money equivalent of the ambiguity
        // the unique index on translations prevents for words.
        builder.HasIndex(p => new { p.VariantId, p.Currency }).IsUnique();

        builder.HasOne<ProductVariant>()
            .WithMany(v => v.Prices)
            .HasForeignKey(p => p.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
