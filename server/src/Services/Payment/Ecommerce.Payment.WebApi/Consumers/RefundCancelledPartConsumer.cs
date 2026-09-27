using Ecommerce.Contracts.Order;
using Ecommerce.Payment.Application.Payments.RefundPart;
using MassTransit;
using MediatR;

namespace Ecommerce.Payment.WebApi.Consumers;

/// <summary>
/// One part of an order was cancelled before it shipped (specs/104): record its refund.
/// </summary>
/// <remarks>
/// ⚠️ Named for what it does - Inventory consumes the same event, and a shared class name would be a shared queue
/// (CLAUDE.md gotcha).
/// </remarks>
public class RefundCancelledPartConsumer(ISender mediator) : IConsumer<OrderPartCancelledEvent>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<OrderPartCancelledEvent> context) =>
        _mediator.Send(new RefundPartCommand(context.Message.PartId, context.Message.OrderId,
            context.Message.Amount, context.Message.Currency), context.CancellationToken);
}
