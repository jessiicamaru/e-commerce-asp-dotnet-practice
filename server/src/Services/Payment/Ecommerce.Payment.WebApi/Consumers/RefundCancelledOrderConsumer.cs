using Ecommerce.Contracts.Order;
using Ecommerce.Payment.Application.Payments.RefundOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Payment.WebApi.Consumers;

/// <summary>
/// A paid order was cancelled (specs/039): record the refund.
/// </summary>
/// <remarks>
/// ⚠️ Named for what it does, not for the event: the class name is the queue name, and Inventory consumes
/// the same event with a class of its own. Two classes of one name would share a queue and each
/// cancellation would reach only one service (CLAUDE.md gotcha, research D6).
/// </remarks>
public class RefundCancelledOrderConsumer(ISender mediator) : IConsumer<OrderCancelledEvent>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<OrderCancelledEvent> context) =>
        _mediator.Send(new RefundOrderCommand(context.Message.OrderId), context.CancellationToken);
}
