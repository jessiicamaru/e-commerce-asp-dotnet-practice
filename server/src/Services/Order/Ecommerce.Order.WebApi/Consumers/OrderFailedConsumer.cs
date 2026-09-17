using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Orders.Commands.FailOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Order.WebApi.Consumers;

/// <summary>
/// Records why an order could not be fulfilled. Nothing consumed <see cref="OrderFailedEvent"/>
/// before this, which is why <c>orders.FailureReason</c> was a column no code ever wrote to and a
/// failed order was indistinguishable from one still in flight.
/// </summary>
/// <remarks>
/// <c>OrderStateMachine</c> publishes this on <b>both</b> failure branches — a stock reservation
/// that could not be met (from <c>Submitted</c>) and a rejected payment (from
/// <c>InventoryReservedState</c>). This consumer therefore covers the reservation path as well,
/// which the original issue did not mention.
/// </remarks>
public class OrderFailedConsumer(ISender mediator, ILogger<OrderFailedConsumer> logger)
    : IConsumer<OrderFailedEvent>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<OrderFailedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OrderFailedEvent> context)
    {
        var settled = await _mediator.Send(
            new FailOrderCommand(
                context.Message.OrderId,
                context.Message.Reason,
                context.Message.FailedAt),
            context.CancellationToken);

        if (!settled)
        {
            _logger.LogDebug(
                "Failure notice for order {OrderId} changed nothing; the handler has logged why.",
                context.Message.OrderId);
        }
    }
}
