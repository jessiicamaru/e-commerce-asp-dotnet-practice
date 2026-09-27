using Ecommerce.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Order.Infrastructure.Persistence.Configurations;

public class DeliveryOptionConfiguration : IEntityTypeConfiguration<DeliveryOption>
{
    public void Configure(EntityTypeBuilder<DeliveryOption> builder)
    {
        builder.ToTable("delivery_options").HasKey(o => o.Code);
        builder.Property(o => o.Code).HasMaxLength(32).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(100).IsRequired();
        builder.Property(o => o.IsActive).IsRequired();
        builder.Property(o => o.SortOrder).IsRequired();

        builder.HasMany(o => o.Prices).WithOne().HasForeignKey(p => p.OptionCode).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeliveryOptionPriceConfiguration : IEntityTypeConfiguration<DeliveryOptionPrice>
{
    public void Configure(EntityTypeBuilder<DeliveryOptionPrice> builder)
    {
        builder.ToTable("delivery_option_prices", t =>
            t.HasCheckConstraint("ck_delivery_option_prices_amount_not_negative", "\"Amount\" >= 0"));
        builder.HasKey(p => new { p.OptionCode, p.Currency });
        builder.Property(p => p.OptionCode).HasMaxLength(32);
        builder.Property(p => p.Currency).HasMaxLength(3);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
    }
}

public class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> builder)
    {
        // One row: the shop has one delivery partner (specs/098).
        builder.ToTable("carriers", t => t.HasCheckConstraint("ck_carriers_one_row", "\"Id\" = 1"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.TrackingUrlTemplate).HasMaxLength(500);
    }
}
