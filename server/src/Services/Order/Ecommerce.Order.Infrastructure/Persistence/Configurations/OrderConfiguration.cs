using Ecommerce.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Order.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Domain.Entities.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(512);

        // Feature 011. Columns on orders, not a table: one per order, never shared, never queried on
        // their own. All nullable - orders placed before this feature have none - which also keeps the
        // migration additive under the constitution's schema rule.
        builder.OwnsOne(x => x.ShipTo, a =>
        {
            a.Property(p => p.RecipientName).HasColumnName("ShipTo_RecipientName").HasMaxLength(100);
            a.Property(p => p.Line1).HasColumnName("ShipTo_Line1").HasMaxLength(200);
            a.Property(p => p.Line2).HasColumnName("ShipTo_Line2").HasMaxLength(200);
            a.Property(p => p.City).HasColumnName("ShipTo_City").HasMaxLength(100);
            a.Property(p => p.Region).HasColumnName("ShipTo_Region").HasMaxLength(100);
            a.Property(p => p.PostalCode).HasColumnName("ShipTo_PostalCode").HasMaxLength(16);
            a.Property(p => p.Country).HasColumnName("ShipTo_Country").HasMaxLength(2);
            a.Property(p => p.Phone).HasColumnName("ShipTo_Phone").HasMaxLength(30);
        });

        builder.Property(x => x.ShippingOptionCode).HasMaxLength(32);
        builder.Property(x => x.ShippingOptionName).HasMaxLength(100);
        builder.Property(x => x.ShippingPrice).HasPrecision(18, 2);
        builder.Property(x => x.TrackingReference).HasMaxLength(100);

        // Feature 012 - the parts of the total, and the rule that they add up, as constraints and not
        // only as code (constitution: invariants that matter are database constraints too). Every
        // check lets a NULL Subtotal through: an image from before this feature writes rows without
        // the parts, and must still be able to (schema evolution rule).
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountTotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 4);
        builder.Property(x => x.CommissionRate).HasPrecision(5, 4);
        builder.Property(x => x.CancelledBy).HasMaxLength(16);

        // Feature 022 - the currency every amount above is in. Nullable, because an order placed
        // before this feature recorded amounts whose currency nobody stated, and writing one in now
        // would be inventing a fact. The money columns stay decimal(18,2) even though dong has no
        // decimals: narrowing them would be a contracting migration that strands an earlier image
        // (research D5).
        builder.Property(x => x.Currency).HasMaxLength(3);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_orders_parts_sum_to_total",
                "\"Subtotal\" IS NULL OR \"Subtotal\" + COALESCE(\"ShippingPrice\", 0) + \"TaxTotal\" - \"DiscountTotal\" = \"TotalAmount\"");
            // Until specs/069 the discount part had to be zero ("CK_orders_no_discount_yet"): a discount appearing
            // before the code that computes one would have been a bug. Now vouchers make one, and what is kept
            // is that it is never negative - the parts still have to sum to the total above.
            t.HasCheckConstraint("CK_orders_discount_not_negative",
                "\"DiscountTotal\" IS NULL OR \"DiscountTotal\" >= 0");
            t.HasCheckConstraint("CK_orders_tax_rate_range",
                "\"TaxRate\" IS NULL OR (\"TaxRate\" >= 0 AND \"TaxRate\" < 1)");
            t.HasCheckConstraint("CK_orders_commission_rate_range",
                "\"CommissionRate\" IS NULL OR (\"CommissionRate\" >= 0 AND \"CommissionRate\" < 1)");
        });

        // Staff work the fulfilment queue by status, oldest first.
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_orders_Status_CreatedAt");

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every order-list request filters on UserId and sorts by CreatedAt descending. Matching
        // the sort direction lets one index satisfy both the filter and the ordering, instead of
        // an index scan followed by a sort.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_orders_UserId_CreatedAt");
    }
}
