using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Contracts.Inventory;
using MassTransit;
using MediatR;

namespace Ecommerce.Catalog.WebApi.Consumers;

/// <summary>
/// Catalog's first consumer. Until now this service only published — which is why
/// <c>Product.StockQuantity</c> held a number assigned at creation that no code could ever change,
/// shown to every anonymous shopper (issue #4).
/// </summary>
/// <remarks>
/// What it records is a <b>read model</b>: Inventory owns stock, this is a copy kept for display.
/// Nothing may sell against it. Checkout still reserves against Inventory's row under
/// <c>FOR UPDATE</c>, and it must stay that way — this answer is seconds behind by design.
/// </remarks>
public class StockAvailabilityChangedConsumer(
    ISender mediator,
    ILogger<StockAvailabilityChangedConsumer> logger
) : IConsumer<StockAvailabilityChangedEvent>
{
    private readonly ISender _mediator = mediator;
    private readonly ILogger<StockAvailabilityChangedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<StockAvailabilityChangedEvent> context)
    {
        var recorded = await _mediator.Send(
            new RecordStockAvailabilityCommand(
                context.Message.ProductId,
                context.Message.IsAvailable,
                context.Message.ObservedAt),
            context.CancellationToken);

        if (!recorded)
        {
            _logger.LogDebug(
                "Availability announcement for product {ProductId} changed nothing; the handler has "
                + "logged why.",
                context.Message.ProductId);
        }
    }
}
