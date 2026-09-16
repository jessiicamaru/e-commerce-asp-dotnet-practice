using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// Answers the saga's request for stock. The handler it dispatches to publishes the reply inside
/// the same transaction as the stock change, so a reply can never exist without its effect.
/// </summary>
public class ReserveInventoryConsumer(ISender mediator, ILogger<ReserveInventoryConsumer> logger)
    : IConsumer<ReserveInventoryCommand>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<ReserveInventoryConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<ReserveInventoryCommand> context)
    {
        var message = context.Message;

        var items = (message.Items ?? [])
            .Select(x => new ReserveStockItem(x.ProductId, x.Quantity))
            .ToList();

        var result = await _mediator.Send(
            new ReserveStockCommand(message.OrderId, items), context.CancellationToken);

        if (result.Succeeded)
        {
            _logger.LogInformation("Reserved stock for order {OrderId}.", message.OrderId);
        }
        else
        {
            _logger.LogWarning(
                "Rejected stock request for order {OrderId}: {Reason}",
                message.OrderId,
                result.FailureReason);
        }
    }
}
