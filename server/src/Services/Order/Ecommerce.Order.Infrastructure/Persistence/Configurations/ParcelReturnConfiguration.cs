using Ecommerce.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Order.Infrastructure.Persistence.Configurations;

public class ParcelReturnConfiguration : IEntityTypeConfiguration<ParcelReturn>
{
    public void Configure(EntityTypeBuilder<ParcelReturn> builder)
    {
        builder.ToTable("parcel_returns");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Text, like every status here: a new value never stops an earlier image reading the row.
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.DecisionReason).HasMaxLength(500);
        builder.Property(r => r.TrackingReference).HasMaxLength(100);
        builder.Property(r => r.RefundAmount).HasPrecision(18, 2);

        // One return per parcel (specs/066): two requests at once insert once (ON CONFLICT on this index).
        builder.HasIndex(r => r.ShipmentId).IsUnique();
        builder.HasIndex(r => new { r.Status, r.RequestedAt });
        builder.HasIndex(r => r.SellerId);

        builder.HasOne(r => r.Shipment)
            .WithOne(s => s.Return)
            .HasForeignKey<ParcelReturn>(r => r.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
