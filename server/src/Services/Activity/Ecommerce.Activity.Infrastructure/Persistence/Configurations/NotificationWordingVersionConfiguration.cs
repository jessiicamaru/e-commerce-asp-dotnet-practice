using Ecommerce.Activity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Activity.Infrastructure.Persistence.Configurations;

public class NotificationWordingVersionConfiguration : IEntityTypeConfiguration<NotificationWordingVersion>
{
    public void Configure(EntityTypeBuilder<NotificationWordingVersion> builder)
    {
        builder.ToTable("notification_wording_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Key).HasMaxLength(80).IsRequired();
        builder.Property(v => v.Language).HasMaxLength(10).IsRequired();
        builder.Property(v => v.Text).HasMaxLength(1000);

        // Two saves at once both ask for N + 1; the index lets one of them have it.
        builder.HasIndex(v => new { v.Key, v.Language, v.Version }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("CK_notification_wording_versions_text", "\"IsDefault\" OR \"Text\" IS NOT NULL"));
    }
}
