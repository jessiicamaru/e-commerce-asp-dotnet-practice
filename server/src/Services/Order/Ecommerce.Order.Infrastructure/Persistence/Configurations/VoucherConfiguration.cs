using Ecommerce.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Order.Infrastructure.Persistence.Configurations;

/// <summary>A voucher and its parts (specs/069). Every enum is text, like every status here.</summary>
public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        // Stored upper-case by the handler, so a plain unique index is case-insensitive in effect.
        builder.Property(v => v.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(v => v.Code).IsUnique();

        builder.Property(v => v.Name).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Benefit).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.Percent).HasPrecision(5, 2);
        builder.HasIndex(v => new { v.SellerId, v.CreatedAt });

        // The counter the total limit guards - never below zero, never past the limit (research D5).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_vouchers_used_count",
            "\"UsedCount\" >= 0 AND (\"TotalLimit\" IS NULL OR \"UsedCount\" <= \"TotalLimit\")"));

        builder.HasMany(v => v.Conditions).WithOne().HasForeignKey(c => c.VoucherId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Targets).WithOne().HasForeignKey(t => t.VoucherId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Amounts).WithOne().HasForeignKey(a => a.VoucherId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VoucherConditionConfiguration : IEntityTypeConfiguration<VoucherCondition>
{
    public void Configure(EntityTypeBuilder<VoucherCondition> builder)
    {
        builder.ToTable("voucher_conditions");
        builder.HasKey(c => new { c.VoucherId, c.Type });
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
    }
}

public class VoucherTargetConfiguration : IEntityTypeConfiguration<VoucherTarget>
{
    public void Configure(EntityTypeBuilder<VoucherTarget> builder)
    {
        builder.ToTable("voucher_targets");
        builder.HasKey(t => new { t.VoucherId, t.Type, t.TargetId });
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(16);
    }
}

public class VoucherAmountConfiguration : IEntityTypeConfiguration<VoucherAmount>
{
    public void Configure(EntityTypeBuilder<VoucherAmount> builder)
    {
        builder.ToTable("voucher_amounts");
        builder.HasKey(a => new { a.VoucherId, a.Currency });
        builder.Property(a => a.Currency).HasMaxLength(3);
        builder.Property(a => a.FixedValue).HasPrecision(18, 2);
        builder.Property(a => a.MaxDiscount).HasPrecision(18, 2);
        builder.Property(a => a.MinSubtotal).HasPrecision(18, 2);
    }
}

public class VoucherCustomerUseConfiguration : IEntityTypeConfiguration<VoucherCustomerUse>
{
    public void Configure(EntityTypeBuilder<VoucherCustomerUse> builder)
    {
        builder.ToTable("voucher_customer_uses");
        builder.HasKey(u => new { u.VoucherId, u.CustomerId });
        builder.ToTable(t => t.HasCheckConstraint("CK_voucher_customer_uses_uses", "\"Uses\" >= 0"));
    }
}

public class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ToTable("voucher_redemptions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Code).HasMaxLength(32).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Benefit).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Amount).HasPrecision(18, 2);
        builder.Property(r => r.Currency).HasMaxLength(3);

        // One use of a voucher per order; releasing one finds it by the order.
        builder.HasIndex(r => new { r.VoucherId, r.OrderId }).IsUnique();
        builder.HasIndex(r => r.OrderId);

        builder.HasOne<Domain.Entities.Order>().WithMany(o => o.Vouchers).HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        // Never cascade from the voucher: a redemption is part of an order's record.
        builder.HasOne<Voucher>().WithMany().HasForeignKey(r => r.VoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}
