using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Configurations;

public class SellerPayoutAccountConfiguration : IEntityTypeConfiguration<SellerPayoutAccount>
{
    public void Configure(EntityTypeBuilder<SellerPayoutAccount> builder)
    {
        builder.ToTable("seller_payout_accounts");
        builder.HasKey(x => x.SellerId);
        builder.Property(x => x.SellerId).ValueGeneratedNever();
        builder.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AccountHolder).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AccountNumber).HasMaxLength(34).IsRequired();
        builder.Ignore(x => x.Masked);
        builder.Ignore(x => x.Last4);
    }
}
