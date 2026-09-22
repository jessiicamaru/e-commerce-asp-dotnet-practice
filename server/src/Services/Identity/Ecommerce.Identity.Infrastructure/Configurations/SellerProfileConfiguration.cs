using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.ToTable("seller_profiles");

        // The user id IS the key. "One shop per account" becomes something the database refuses
        // rather than something a handler has to remember (specs/027).
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).ValueGeneratedNever();

        builder.Property(p => p.ShopName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<SellerProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
