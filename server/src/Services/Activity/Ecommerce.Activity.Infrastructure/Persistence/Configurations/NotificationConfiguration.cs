using Ecommerce.Activity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Activity.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Data).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Link).HasMaxLength(256);

        // An inbox is read newest first; the bell counts the unread.
        builder.HasIndex(x => new { x.RecipientId, x.CreatedAt });
        builder.HasIndex(x => x.RecipientId).HasFilter("\"ReadAt\" IS NULL").HasDatabaseName("IX_notifications_unread");
    }
}
