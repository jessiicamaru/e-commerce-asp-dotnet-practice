using Ecommerce.Contracts.Order;
using Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// One part of a paid order was cancelled before it shipped (specs/104): put back what that part took - its variants'
/// reservations, released if still held and returned to the shelf if confirmed, exactly as a whole cancellation does.
/// </summary>
/// <remarks>⚠️ Named for what it does: Payment consumes the same event, and a shared class name would be a shared queue.</remarks>
public class RestockCancelledPartConsumer(ISender mediator, ILogger<RestockCancelledPartConsumer> logger)
    : IConsumer<OrderPartCancelledEvent>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<RestockCancelledPartConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OrderPartCancelledEvent> context)
    {
        var settled = await _mediator.Send(
            new RestockCancelledOrderCommand(context.Message.OrderId, context.Message.VariantIds), context.CancellationToken);

        _logger.LogInformation("Put back {Count} reservation(s) of a cancelled part of order {OrderId}.", settled, context.Message.OrderId);
    }
}
