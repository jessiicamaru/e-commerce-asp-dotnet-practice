using Ecommerce.Payment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Payment.Infrastructure.Persistence.Configurations;

public class PaymentCheckoutConfiguration : IEntityTypeConfiguration<PaymentCheckout>
{
    public void Configure(EntityTypeBuilder<PaymentCheckout> builder)
    {
        builder.ToTable("payment_checkouts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseCode).HasMaxLength(8);
        builder.Property(x => x.ProviderReference).HasMaxLength(64);

        // One checkout per order, and one order per reference: the gateway's notification finds exactly one.
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.Reference).IsUnique();
    }
}
