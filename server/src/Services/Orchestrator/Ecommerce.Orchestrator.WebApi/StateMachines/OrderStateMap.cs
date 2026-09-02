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

        entity.Property(x => x.CreatedAt)
            .IsRequired();

        entity.Property(x => x.UpdatedAt)
            .IsRequired();
    }
}
