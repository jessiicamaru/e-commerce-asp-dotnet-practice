using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class SellerConfiguration : IEntityTypeConfiguration<Seller>
{
    public void Configure(EntityTypeBuilder<Seller> builder)
    {
        builder.ToTable("sellers").HasKey(s => s.SellerId);

        // The id comes from Identity, so nothing here generates it.
        builder.Property(s => s.SellerId).ValueGeneratedNever();
        builder.Property(s => s.ShopName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.ObservedAt).IsRequired();
    }
}
