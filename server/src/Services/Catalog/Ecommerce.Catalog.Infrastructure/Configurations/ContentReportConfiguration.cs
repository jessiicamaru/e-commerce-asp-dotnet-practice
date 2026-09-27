using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Configurations;

public class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("content_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Text, as every enum here. No HasDefaultValue: an enum's first value is its CLR default, and EF would leave
        // it out of the INSERT (the specs/093 trap).
        builder.Property(r => r.TargetType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Details).HasMaxLength(500);

        // One open report per person per thing; once decided, they may report it again.
        builder.HasIndex(r => new { r.TargetType, r.TargetId, r.ReporterId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Open'")
            .HasDatabaseName("UX_content_reports_open_per_reporter");
        // The queue: what is open, per thing.
        builder.HasIndex(r => new { r.TargetType, r.TargetId }).HasFilter("\"Status\" = 'Open'");

        builder.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
