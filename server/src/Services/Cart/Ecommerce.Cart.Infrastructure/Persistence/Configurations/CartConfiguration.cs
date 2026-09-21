using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Cart.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Domain.Entities.Cart>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();   // see CartLineConfiguration

        // One cart per customer, enforced by the database. Two first-ever adds racing each other
        // cannot create two carts, because the second insert does nothing on this conflict.
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.HasMany(c => c.Lines)
            .WithOne()
            .HasForeignKey(l => l.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
