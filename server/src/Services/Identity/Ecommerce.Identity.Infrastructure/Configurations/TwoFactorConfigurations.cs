using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class TwoFactorRecoveryCodeConfiguration : IEntityTypeConfiguration<TwoFactorRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TwoFactorRecoveryCode> builder)
    {
        builder.ToTable("two_factor_recovery_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // The hash, never the code (specs/110 research D6).
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(c => new { c.UserId, c.CodeHash });

        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TwoFactorChallengeConfiguration : IEntityTypeConfiguration<TwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<TwoFactorChallenge> builder)
    {
        builder.ToTable("two_factor_challenges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => c.UserId);

        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
