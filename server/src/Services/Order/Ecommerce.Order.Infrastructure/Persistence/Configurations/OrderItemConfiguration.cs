using Ecommerce.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Order.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(x => x.Id);

        // Frozen copies of what was bought (specs/020). Nullable: lines written before variants have
        // none, and those are the product's only variant.
        builder.Property(x => x.Sku).HasMaxLength(50);
        builder.Property(x => x.OptionSummary).HasMaxLength(200);

        builder.Property(x => x.ProductName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasPrecision(18, 2);
    }
}
