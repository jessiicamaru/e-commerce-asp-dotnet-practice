using Ecommerce.Payment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Payment.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds", t => t.HasCheckConstraint("CK_refunds_amount_positive", "\"Amount\" > 0"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();

        // One refund of the WHOLE order: the second delivery of a cancellation is a unique violation, not a
        // second refund (research D3). Since specs/066 an order may also have one refund per returned parcel,
        // so the rule is partial - and each return is refunded once, by its own index.
        builder.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"ReturnId\" IS NULL");
        builder.HasIndex(x => x.ReturnId).IsUnique().HasFilter("\"ReturnId\" IS NOT NULL");

        builder.HasOne<Domain.Entities.Payment>()
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
