using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Orchestrator.WebApi.StateMachines;

public class OrderStateMap : SagaClassMap<OrderStateData>
{
    protected override void Configure(EntityTypeBuilder<OrderStateData> entity, ModelBuilder model)
    {
        entity.ToTable("order_state_data");

        entity.Property(x => x.CurrentState)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.UserId)
            .IsRequired();

        entity.Property(x => x.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        entity.Property(x => x.FailureReason)
            .HasMaxLength(512);

        // What TotalAmount is denominated in (specs/022). Nullable: a saga instance already in
        // flight when this deployed carries none, and reads as the shop's default.
        entity.Property(x => x.Currency)
            .HasMaxLength(3);

        entity.Property(x => x.CreatedAt)
            .IsRequired();

        entity.Property(x => x.UpdatedAt)
            .IsRequired();
    }
}
