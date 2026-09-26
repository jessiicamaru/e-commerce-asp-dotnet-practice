using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class EmailTemplateVersionConfiguration : IEntityTypeConfiguration<EmailTemplateVersion>
{
    public void Configure(EntityTypeBuilder<EmailTemplateVersion> builder)
    {
        builder.ToTable("email_template_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Template).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Language).HasMaxLength(10).IsRequired();
        builder.Property(v => v.Subject).HasMaxLength(200);
        builder.Property(v => v.BodyHtml).HasMaxLength(20000);

        // One version number per template and language: two administrators saving at once both ask for N + 1, and
        // the index lets exactly one of them have it.
        builder.HasIndex(v => new { v.Template, v.Language, v.Version }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_email_template_versions_words",
            "\"IsDefault\" OR (\"Subject\" IS NOT NULL AND \"BodyHtml\" IS NOT NULL)"));
    }
}
