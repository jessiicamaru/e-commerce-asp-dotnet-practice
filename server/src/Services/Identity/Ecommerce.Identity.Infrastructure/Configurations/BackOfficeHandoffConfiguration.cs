using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class BackOfficeHandoffConfiguration : IEntityTypeConfiguration<BackOfficeHandoff>
{
    public void Configure(EntityTypeBuilder<BackOfficeHandoff> builder)
    {
        builder.ToTable("back_office_handoffs");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        // The hash, never the code (specs/140): 64 hex characters, and what a redemption looks the row up by.
        builder.Property(h => h.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(h => h.CodeHash).IsUnique();

        builder.HasIndex(h => h.UserId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
