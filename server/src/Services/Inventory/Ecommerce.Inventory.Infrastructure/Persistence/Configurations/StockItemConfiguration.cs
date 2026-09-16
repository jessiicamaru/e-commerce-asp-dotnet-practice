using Ecommerce.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Inventory.Infrastructure.Persistence.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items", t =>
        {
            // The last line of defence against a concurrency bug. Without these a mistake in the
            // reservation path oversells silently; with them it fails loudly at the database.
            t.HasCheckConstraint("ck_stock_items_on_hand_non_negative", "\"QuantityOnHand\" >= 0");
            t.HasCheckConstraint(
                "ck_stock_items_reserved_within_on_hand",
                "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).IsRequired();
        builder.HasIndex(x => x.ProductId).IsUnique();

        builder.Property(x => x.Sku).HasMaxLength(50).IsRequired();

        builder.Property(x => x.QuantityOnHand).IsRequired();
        builder.Property(x => x.QuantityReserved).IsRequired();

        // Derived in the domain; never a column.
        builder.Ignore(x => x.QuantityAvailable);
    }
}
