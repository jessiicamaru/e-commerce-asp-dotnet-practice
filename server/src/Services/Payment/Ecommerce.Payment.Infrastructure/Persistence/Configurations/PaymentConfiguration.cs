using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Payment.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Domain.Entities.Payment>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.FailureReason).HasMaxLength(512);

        builder.Property(x => x.Provider)
            .HasMaxLength(32)
            .HasDefaultValue(Domain.Entities.Payment.StubProvider)
            .IsRequired();

        // The guarantee behind FR-005 and FR-006, not an optimisation: this is what stops two
        // simultaneous requests both inserting a payment for one order.
        builder.HasIndex(x => x.OrderId).IsUnique();

        // Deliberately no check constraint on Amount. A rejection for a non-positive amount must
        // still be recorded, with the offending amount visible — that record is the point.
    }
}
