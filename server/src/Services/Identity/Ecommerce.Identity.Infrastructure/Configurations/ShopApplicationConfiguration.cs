using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class ShopApplicationConfiguration : IEntityTypeConfiguration<ShopApplication>
{
    public void Configure(EntityTypeBuilder<ShopApplication> builder)
    {
        builder.ToTable("shop_applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ShopName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(1000);
        builder.Property(a => a.Phone).HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.DecisionReason).HasMaxLength(500);

        // One pending application per person: the database refuses the second, whatever raced.
        builder.HasIndex(a => a.UserId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'")
            .HasDatabaseName("IX_shop_applications_one_pending");

        // The queue: pending, oldest first.
        builder.HasIndex(a => new { a.Status, a.CreatedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
