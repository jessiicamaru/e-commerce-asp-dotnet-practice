using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class SavedProductConfiguration : IEntityTypeConfiguration<SavedProduct>
{
    public void Configure(EntityTypeBuilder<SavedProduct> builder)
    {
        builder.ToTable("saved_products");

        // Once per shopper per product: saving twice - or twice at once - inserts once (ON CONFLICT on this key).
        builder.HasKey(s => new { s.CustomerId, s.ProductId });
        // The list is the shopper's, newest first; "who saved this" is the back-in-stock notice's question.
        builder.HasIndex(s => new { s.CustomerId, s.SavedAt });
        builder.HasIndex(s => s.ProductId);

        builder.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
