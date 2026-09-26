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

        // What vouchers took off (specs/069). Defaulted in the DATABASE, so an older image writing a line
        // without them writes 0 rather than failing - an expand-only change.
        builder.Property(x => x.ShopDiscount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.PlatformDiscount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Ignore(x => x.NetPrice);
        builder.ToTable(t => t.HasCheckConstraint("CK_order_items_discounts",
            "\"ShopDiscount\" >= 0 AND \"PlatformDiscount\" >= 0 AND \"ShopDiscount\" + \"PlatformDiscount\" <= \"UnitPrice\" * \"Quantity\""));

        // Both of a seller's reads start from "lines of this seller" (specs/034). Without this, every
        // page a seller opens scans every order line in the shop.
        builder.HasIndex(x => x.SellerId);

        // A shop name, frozen at checkout (specs/036). Catalog's own column is 100 wide.
        builder.Property(x => x.SellerName).HasMaxLength(100);
    }
}
