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
        // A reply that finds no saga instance used to be DISCARDED IN SILENCE - no fault, no error queue,
        // no log line. That is how the 2026-09-21 stall hid for eighteen days: InventoryReservedEvent
        // arrived before the instance it answered was committed, and vanished. It is still discarded
        // (redelivering it would fault forever), but it now says so, at Warning (feature 013).
        Event(() => InventoryReserved, x => { x.CorrelateById(m => m.Message.OrderId); x.OnMissingInstance(m => m.Execute(ctx => MissingInstance(nameof(InventoryReservedEvent), ctx.Message.OrderId))); });
        Event(() => InventoryReservationFailed, x => { x.CorrelateById(m => m.Message.OrderId); x.OnMissingInstance(m => m.Execute(ctx => MissingInstance(nameof(InventoryReservationFailedEvent), ctx.Message.OrderId))); });
        Event(() => PaymentProcessed, x => { x.CorrelateById(m => m.Message.OrderId); x.OnMissingInstance(m => m.Execute(ctx => MissingInstance(nameof(PaymentProcessedEvent), ctx.Message.OrderId))); });
        Event(() => PaymentFailed, x => { x.CorrelateById(m => m.Message.OrderId); x.OnMissingInstance(m => m.Execute(ctx => MissingInstance(nameof(PaymentFailedEvent), ctx.Message.OrderId))); });

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.CreatedAt = DateTime.UtcNow;
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                    Transition(context.Message.OrderId, "submitted; reserving inventory");
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
                    Transition(context.Message.OrderId, "inventory reserved; requesting payment");
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
                    Transition(context.Message.OrderId, $"reservation failed ({context.Message.Reason}); order failed");
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
                    Transition(context.Message.OrderId, "payment approved; order completed");
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
                    Transition(context.Message.OrderId, $"payment failed ({context.Message.Reason}); releasing inventory, order failed");
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

    // Every transition at Information, with the order id as a property: readable in production without
    // switching MassTransit to Debug, which is what diagnosing the 2026-09-21 stall required (feature 013).
    private static void Transition(Guid orderId, string what) =>
        LogContext.Info?.Log("Saga {OrderId}: {Transition}", orderId, what);

    private static void MissingInstance(string eventName, Guid orderId) =>
        LogContext.Warning?.Log(
            "Saga {OrderId}: {Event} arrived but no saga instance exists for it, so it was discarded. "
            + "If the order is still Submitted, the reply overtook the submission's commit - check that "
            + "the orchestrator publishes through the transactional outbox.",
            orderId, eventName);
}
