using Ecommerce.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Inventory.Infrastructure.Persistence.Configurations;

public class ReturnedParcelConfiguration : IEntityTypeConfiguration<ReturnedParcel>
{
    public void Configure(EntityTypeBuilder<ReturnedParcel> builder)
    {
        builder.ToTable("returned_parcels");

        // The return's id is the key: the claim is INSERT ... ON CONFLICT DO NOTHING on it (specs/066).
        builder.HasKey(x => x.ReturnId);
        builder.Property(x => x.ReturnId).ValueGeneratedNever();
        builder.HasIndex(x => x.OrderId);
    }
}
