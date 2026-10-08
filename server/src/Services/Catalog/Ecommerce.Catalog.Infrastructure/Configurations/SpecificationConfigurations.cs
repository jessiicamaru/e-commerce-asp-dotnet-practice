using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

// Product specifications (specs/159, #366): five tables, added by one expand-only migration.

public class CategorySpecificationConfiguration : IEntityTypeConfiguration<CategorySpecification>
{
    public void Configure(EntityTypeBuilder<CategorySpecification> builder)
    {
        builder.ToTable("category_specifications").HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(60).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.HasIndex(s => new { s.CategoryId, s.Code }).IsUnique();

        // A category can only be deleted empty (specs/024, specs/158); what it declared goes with it.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CategorySpecificationTranslationConfiguration : IEntityTypeConfiguration<CategorySpecificationTranslation>
{
    public void Configure(EntityTypeBuilder<CategorySpecificationTranslation> builder)
    {
        builder.ToTable("category_specification_translations").HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Language).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => new { t.SpecificationId, t.Language }).IsUnique();

        builder.HasOne<CategorySpecification>()
            .WithMany(s => s.Translations)
            .HasForeignKey(t => t.SpecificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SpecificationOptionConfiguration : IEntityTypeConfiguration<SpecificationOption>
{
    public void Configure(EntityTypeBuilder<SpecificationOption> builder)
    {
        builder.ToTable("specification_options").HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Code).HasMaxLength(60).IsRequired();
        builder.Property(o => o.Value).HasMaxLength(100).IsRequired();

        builder.HasIndex(o => new { o.SpecificationId, o.Code }).IsUnique();

        builder.HasOne<CategorySpecification>()
            .WithMany(s => s.Options)
            .HasForeignKey(o => o.SpecificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SpecificationOptionTranslationConfiguration : IEntityTypeConfiguration<SpecificationOptionTranslation>
{
    public void Configure(EntityTypeBuilder<SpecificationOptionTranslation> builder)
    {
        builder.ToTable("specification_option_translations").HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Language).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Value).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => new { t.OptionId, t.Language }).IsUnique();

        builder.HasOne<SpecificationOption>()
            .WithMany(o => o.Translations)
            .HasForeignKey(t => t.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductSpecificationConfiguration : IEntityTypeConfiguration<ProductSpecification>
{
    public void Configure(EntityTypeBuilder<ProductSpecification> builder)
    {
        builder.ToTable("product_specifications", t =>
            // A value is an option or a text, never both and never neither.
            t.HasCheckConstraint("CK_product_specifications_one_value",
                "(\"OptionId\" IS NOT NULL AND \"Text\" IS NULL) OR (\"OptionId\" IS NULL AND \"Text\" IS NOT NULL)"));
        builder.HasKey(v => new { v.ProductId, v.SpecificationId });
        builder.Property(v => v.Text).HasMaxLength(200);

        // The listing's filter asks "which products have this option" (specs/159 research D5).
        builder.HasIndex(v => v.OptionId);

        // Deleting a product takes its values, as it takes its variants.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // What a product uses is not deleted under it: the handlers check first and answer 409.
        builder.HasOne<CategorySpecification>()
            .WithMany()
            .HasForeignKey(v => v.SpecificationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpecificationOption>()
            .WithMany()
            .HasForeignKey(v => v.OptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
