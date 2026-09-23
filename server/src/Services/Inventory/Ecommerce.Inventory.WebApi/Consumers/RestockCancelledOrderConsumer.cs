using Ecommerce.Contracts.Order;
using Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// A paid order was cancelled (specs/039): put back what it took.
/// </summary>
/// <remarks>
/// ⚠️ Named for what it does, not for the event. The class name becomes the queue name, and Payment
/// consumes the same event: two classes called <c>OrderCancelledConsumer</c> would bind to ONE queue and
/// each cancellation would reach only one of the two services (CLAUDE.md gotcha, research D6).
/// </remarks>
public class RestockCancelledOrderConsumer(ISender mediator, ILogger<RestockCancelledOrderConsumer> logger)
    : IConsumer<OrderCancelledEvent>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<RestockCancelledOrderConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var settled = await _mediator.Send(
            new RestockCancelledOrderCommand(context.Message.OrderId), context.CancellationToken);

        _logger.LogInformation(
            "Put back {Count} reservation(s) for cancelled order {OrderId}.", settled, context.Message.OrderId);
    }
}
