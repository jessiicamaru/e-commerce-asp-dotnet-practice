using Ecommerce.Contracts.Catalog;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

public class ProductCreatedConsumer(ISender mediator) : IConsumer<ProductCreatedEvent>
{
    private readonly ISender _mediator = mediator;

    public async Task Consume(ConsumeContext<ProductCreatedEvent> context)
    {
        await _mediator.Send(
            new RegisterProductCommand(context.Message.ProductId, context.Message.Sku),
            context.CancellationToken);
    }
}
