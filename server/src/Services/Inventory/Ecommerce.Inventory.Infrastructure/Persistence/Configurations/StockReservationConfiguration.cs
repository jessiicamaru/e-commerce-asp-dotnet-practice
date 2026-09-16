using Ecommerce.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Inventory.Infrastructure.Persistence.Configurations;

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations", t =>
            t.HasCheckConstraint("ck_stock_reservations_quantity_positive", "\"Quantity\" > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.SettlementReason).HasMaxLength(512);

        // The business-level idempotency guard: a redelivered reserve command cannot create a
        // second row for the same order and product, however it reaches us.
        builder.HasIndex(x => new { x.OrderId, x.ProductId }).IsUnique();

        builder.HasIndex(x => x.OrderId);

        // Keeps the expiry sweeper off settled rows.
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}
