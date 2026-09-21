using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Cart.Infrastructure.Persistence.Configurations;

public class CheckoutOutcomeConfiguration : IEntityTypeConfiguration<Domain.Entities.CheckoutOutcome>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.CheckoutOutcome> builder)
    {
        builder.ToTable("checkout_outcomes");

        // One row per order, whichever of the three events creates it.
        builder.HasKey(o => o.OrderId);

        builder.Property(o => o.ItemsJson).HasColumnType("jsonb");

        builder.Property(o => o.Outcome)
            .HasConversion<string>()
            .HasMaxLength(16);
    }
}
