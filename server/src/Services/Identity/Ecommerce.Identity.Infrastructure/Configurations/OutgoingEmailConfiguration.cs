using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class OutgoingEmailConfiguration : IEntityTypeConfiguration<OutgoingEmail>
{
    public void Configure(EntityTypeBuilder<OutgoingEmail> builder)
    {
        builder.ToTable("outgoing_emails");

        builder.HasKey(e => e.Id);
        // The requester's id, never generated here - it is what makes a redelivery a no-op.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Template).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DataJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Language).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(1000);

        // What the sweeper asks: pending, due, oldest first - over the pending rows only.
        builder.HasIndex(e => e.NextAttemptAt)
            .HasFilter("\"Status\" = 'Pending'")
            .HasDatabaseName("IX_outgoing_emails_due");

        builder.HasIndex(e => e.RecipientId);
    }
}
