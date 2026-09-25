using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class SignInThrottleConfiguration : IEntityTypeConfiguration<SignInThrottle>
{
    public void Configure(EntityTypeBuilder<SignInThrottle> builder)
    {
        builder.ToTable("sign_in_throttles");

        // The email as compared, not an account id (specs/062): unknown addresses are counted too (#28).
        builder.HasKey(t => t.EmailKey);
        builder.Property(t => t.EmailKey).HasMaxLength(255).ValueGeneratedNever();
    }
}
