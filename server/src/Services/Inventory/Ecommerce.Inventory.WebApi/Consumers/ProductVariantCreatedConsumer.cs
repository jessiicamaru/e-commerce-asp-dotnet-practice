using Ecommerce.Contracts.Catalog;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// A new shape of a product exists, so it needs a place to count its stock (specs/020).
/// </summary>
/// <remarks>
/// Registered at zero on hand, like a new product: a thing nobody has counted yet is not buyable, and
/// the announcement that follows says exactly that. Idempotent - a redelivery finds the row already
/// there and changes nothing.
/// </remarks>
public class ProductVariantCreatedConsumer(ISender mediator) : IConsumer<ProductVariantCreatedEvent>
{
    private readonly ISender _mediator = mediator;

    public async Task Consume(ConsumeContext<ProductVariantCreatedEvent> context)
    {
        await _mediator.Send(
            new RegisterProductCommand(context.Message.VariantId, context.Message.Sku),
            context.CancellationToken);
    }
}
