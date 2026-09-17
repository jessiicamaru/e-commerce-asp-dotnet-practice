using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Orders.Commands.CompleteOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Order.WebApi.Consumers;

/// <summary>
/// The first thing in this service that listens. Until now <c>Ecommerce.Order</c> published
/// <c>OrderSubmittedEvent</c> and then stopped taking part, so every order row read
/// <c>Submitted</c> however the checkout actually ended.
/// </summary>
/// <remarks>
/// <see cref="OrderCompletedEvent"/> is <b>already consumed</b> by
/// <c>Ecommerce.Inventory</c>, which uses it as its confirmation signal. Adding a second subscriber
/// does not take it away: MassTransit fans a published message out to every subscribing service's
/// own queue. Worth stating, because "that event is already handled" is a natural reason to think it
/// cannot be handled again.
/// </remarks>
public class OrderCompletedConsumer(ISender mediator, ILogger<OrderCompletedConsumer> logger)
    : IConsumer<OrderCompletedEvent>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<OrderCompletedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var settled = await _mediator.Send(
            new CompleteOrderCommand(context.Message.OrderId, context.Message.CompletedAt),
            context.CancellationToken);

        if (!settled)
        {
            _logger.LogDebug(
                "Completion notice for order {OrderId} changed nothing; the handler has logged why.",
                context.Message.OrderId);
        }
    }
}
