using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Application.Reservations.ReleaseStock;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

public class ReleaseInventoryConsumer(ISender mediator, ILogger<ReleaseInventoryConsumer> logger)
    : IConsumer<ReleaseInventoryCommand>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<ReleaseInventoryConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<ReleaseInventoryCommand> context)
    {
        var released = await _mediator.Send(
            new ReleaseStockCommand(context.Message.OrderId, context.Message.Reason),
            context.CancellationToken);

        _logger.LogInformation(
            "Released {Count} reservation(s) for order {OrderId}.",
            released,
            context.Message.OrderId);
    }
}
