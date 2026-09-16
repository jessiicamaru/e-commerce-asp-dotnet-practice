using Ecommerce.Contracts.Order;
using Ecommerce.Inventory.Application.Reservations.ConfirmStock;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// Closes the confirmation gap. OrderStateMachine publishes OrderCompletedEvent and finalizes
/// without telling inventory anything, so without this consumer a successful order would keep its
/// units held until the expiry sweeper returned them to the shelf — re-selling goods already
/// shipped. There is no ConfirmInventoryCommand in the contracts; this event is the signal.
/// </summary>
public class OrderCompletedConsumer(ISender mediator, ILogger<OrderCompletedConsumer> logger)
    : IConsumer<OrderCompletedEvent>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<OrderCompletedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var confirmed = await _mediator.Send(
            new ConfirmStockCommand(context.Message.OrderId), context.CancellationToken);

        _logger.LogInformation(
            "Confirmed {Count} reservation(s) for completed order {OrderId}.",
            confirmed,
            context.Message.OrderId);
    }
}
