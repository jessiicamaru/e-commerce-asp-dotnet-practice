using Ecommerce.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Order.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Domain.Entities.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(512);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every order-list request filters on UserId and sorts by CreatedAt descending. Matching
        // the sort direction lets one index satisfy both the filter and the ordering, instead of
        // an index scan followed by a sort.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_orders_UserId_CreatedAt");
    }
}
