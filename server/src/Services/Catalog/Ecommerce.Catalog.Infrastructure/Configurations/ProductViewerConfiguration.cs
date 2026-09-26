using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ProductViewerConfiguration : IEntityTypeConfiguration<ProductViewer>
{
    public void Configure(EntityTypeBuilder<ProductViewer> builder)
    {
        builder.ToTable("product_viewers");
        // The key is the claim: the one insert that succeeds for a viewer on a day is the one view counted.
        builder.HasKey(v => new { v.ProductId, v.Day, v.Viewer });
        builder.Property(v => v.Viewer).HasMaxLength(64);
        builder.HasOne<Product>().WithMany().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
