using Ecommerce.Contracts.Inventory;
using Ecommerce.Contracts.Order;
using Ecommerce.Contracts.Payment;
using MassTransit;

namespace Ecommerce.Orchestrator.WebApi.StateMachines;

public class OrderStateMachine : MassTransitStateMachine<OrderStateData>
{
    // States
    public State Submitted { get; private set; } = null!;
    public State InventoryReservedState { get; private set; } = null!;

    // Events
    public Event<OrderSubmittedEvent> OrderSubmitted { get; private set; } = null!;
    public Event<InventoryReservedEvent> InventoryReserved { get; private set; } = null!;
    public Event<InventoryReservationFailedEvent> InventoryReservationFailed { get; private set; } = null!;
    public Event<PaymentProcessedEvent> PaymentProcessed { get; private set; } = null!;
    public Event<PaymentFailedEvent> PaymentFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservationFailed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentProcessed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.CreatedAt = DateTime.UtcNow;
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .Publish(context => new ReserveInventoryCommand(
                    context.Message.OrderId,
                    context.Message.Items
                ))
                .TransitionTo(Submitted)
        );

        During(Submitted,
            When(InventoryReserved)
                .Then(context =>
                {
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .Publish(context => new ProcessPaymentCommand(
                    context.Message.OrderId,
                    context.Saga.UserId,
                    context.Saga.TotalAmount
                ))
                .TransitionTo(InventoryReservedState),

            When(InventoryReservationFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .Publish(context => new OrderFailedEvent(
                    context.Message.OrderId,
                    context.Message.Reason,
                    DateTime.UtcNow
                ))
                .Finalize()
        );

        During(InventoryReservedState,
            When(PaymentProcessed)
                .Then(context =>
                {
                    context.Saga.PaymentId = context.Message.PaymentId;
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .Publish(context => new OrderCompletedEvent(
                    context.Message.OrderId,
                    DateTime.UtcNow
                ))
                .Finalize(),

            When(PaymentFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                })
                // Compensating Transaction: Release reserved inventory
                .Publish(context => new ReleaseInventoryCommand(
                    context.Message.OrderId,
                    $"Payment failed: {context.Message.Reason}"
                ))
                .Publish(context => new OrderFailedEvent(
                    context.Message.OrderId,
                    context.Message.Reason,
                    DateTime.UtcNow
                ))
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }
}
