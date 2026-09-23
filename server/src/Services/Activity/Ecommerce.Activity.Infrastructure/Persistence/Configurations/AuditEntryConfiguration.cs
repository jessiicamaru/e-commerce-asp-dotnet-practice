using Ecommerce.Activity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Activity.Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Category).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorEmail).HasMaxLength(256);
        builder.Property(x => x.ActorRole).HasMaxLength(32);
        builder.Property(x => x.SubjectType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SubjectId).HasMaxLength(128);
        builder.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Service).HasMaxLength(32).IsRequired();

        builder.Property(x => x.Before).HasColumnType("jsonb");
        builder.Property(x => x.After).HasColumnType("jsonb");
        builder.Property(x => x.Changes).HasColumnType("jsonb").IsRequired();

        // The log is read newest first, by category, by actor and by subject.
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.Category, x.OccurredAt });
        builder.HasIndex(x => new { x.ActorId, x.OccurredAt });
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId });
    }
}
