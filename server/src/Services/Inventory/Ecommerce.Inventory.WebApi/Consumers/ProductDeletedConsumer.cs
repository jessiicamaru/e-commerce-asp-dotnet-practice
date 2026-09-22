using Ecommerce.Contracts.Catalog;
using Ecommerce.Inventory.Application.Stock.Commands.ForgetProduct;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// The catalogue has forgotten a product, so there is nothing left to count (specs/024).
/// </summary>
/// <remarks>
/// Without this the stock rows outlive the product: <c>GET /api/stock/{id}</c> keeps answering for
/// something the catalogue has never heard of, and the units sit on the shelf forever. Idempotent -
/// a redelivery finds nothing to forget and says so by doing nothing.
/// </remarks>
public class ProductDeletedConsumer(ISender mediator) : IConsumer<ProductDeletedEvent>
{
    private readonly ISender _mediator = mediator;

    public async Task Consume(ConsumeContext<ProductDeletedEvent> context)
    {
        await _mediator.Send(
            new ForgetProductCommand(context.Message.VariantIds),
            context.CancellationToken);
    }
}
