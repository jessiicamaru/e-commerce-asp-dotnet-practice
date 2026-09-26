using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ProductQuestionConfiguration : IEntityTypeConfiguration<ProductQuestion>
{
    public void Configure(EntityTypeBuilder<ProductQuestion> builder)
    {
        builder.ToTable("product_questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.AskerName).HasMaxLength(100).IsRequired();
        builder.Property(q => q.Body).HasMaxLength(1000).IsRequired();
        builder.Property(q => q.HiddenReason).HasMaxLength(500);
        builder.Property(q => q.Answer).HasMaxLength(2000);
        builder.Property(q => q.AnswerHiddenReason).HasMaxLength(500);

        // A product's page, newest first; and a seller's queue of what is still unanswered.
        builder.HasIndex(q => new { q.ProductId, q.CreatedAt });
        builder.HasIndex(q => q.CreatedAt).HasFilter("\"AnsweredAt\" IS NULL AND \"HiddenAt\" IS NULL");

        builder.HasOne<Product>().WithMany().HasForeignKey(q => q.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
