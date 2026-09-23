using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("product_reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.AuthorName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Body).HasMaxLength(2000);
        builder.Property(r => r.HiddenReason).HasMaxLength(500);
        builder.ToTable(t => t.HasCheckConstraint("CK_product_reviews_rating", "\"Rating\" BETWEEN 1 AND 5"));

        // One review per customer per product: a second one is an edit of the first.
        builder.HasIndex(r => new { r.ProductId, r.CustomerId }).IsUnique();
        builder.HasIndex(r => new { r.ProductId, r.CreatedAt });

        builder.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReviewEligibilityConfiguration : IEntityTypeConfiguration<ReviewEligibility>
{
    public void Configure(EntityTypeBuilder<ReviewEligibility> builder)
    {
        builder.ToTable("review_eligibility");
        builder.HasKey(e => new { e.ProductId, e.CustomerId });
    }
}
