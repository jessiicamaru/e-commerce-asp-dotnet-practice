using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Contracts.Identity;
using MassTransit;
using MediatR;

namespace Ecommerce.Catalog.WebApi.Consumers;

/// <summary>
/// A new shop exists, so the catalogue learns its name (specs/027).
/// </summary>
/// <remarks>
/// This is what keeps a product listing from costing a call per card. Idempotent: a redelivery
/// records the same name at the same instant and changes nothing.
/// </remarks>
public class SellerRegisteredConsumer(ISender mediator) : IConsumer<SellerRegisteredEvent>
{
    private readonly ISender _mediator = mediator;

    public async Task Consume(ConsumeContext<SellerRegisteredEvent> context) =>
        await _mediator.Send(
            new RecordSellerCommand(context.Message.SellerId, context.Message.ShopName, context.Message.RegisteredAt),
            context.CancellationToken);
}

/// <summary>
/// A shop changed its name. <b>No product is touched</b> - that is the point of the read model.
/// </summary>
public class SellerRenamedConsumer(ISender mediator) : IConsumer<SellerRenamedEvent>
{
    private readonly ISender _mediator = mediator;

    public async Task Consume(ConsumeContext<SellerRenamedEvent> context) =>
        await _mediator.Send(
            new RecordSellerCommand(context.Message.SellerId, context.Message.ShopName, context.Message.RenamedAt),
            context.CancellationToken);
}
