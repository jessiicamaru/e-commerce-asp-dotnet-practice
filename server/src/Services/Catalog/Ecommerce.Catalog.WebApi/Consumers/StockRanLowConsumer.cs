using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Contracts.Inventory;
using MassTransit;
using MediatR;

namespace Ecommerce.Catalog.WebApi.Consumers;

/// <summary>
/// Tells a seller a variant is running low (#200, specs/102). Inventory decides the crossing; Catalog knows the seller
/// and the words. The endpoint's inbox makes a redelivered event one notice.
/// </summary>
public class StockRanLowConsumer(ISender mediator) : IConsumer<StockRanLowEvent>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<StockRanLowEvent> context) =>
        _mediator.Send(new NotifyLowStockCommand(context.Message.VariantId, context.Message.QuantityAvailable),
            context.CancellationToken);
}
