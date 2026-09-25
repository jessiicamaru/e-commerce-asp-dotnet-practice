using Ecommerce.Contracts.Order;
using Ecommerce.Inventory.Application.Reservations.RestockReturnedParcel;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.WebApi.Consumers;

/// <summary>
/// A returned parcel came back to its seller (specs/066): put its units back on the shelf.
/// </summary>
/// <remarks>
/// ⚠️ Named for what it does - Payment consumes the same event, and a shared class name would be a shared queue
/// (CLAUDE.md gotcha).
/// </remarks>
public class RestockReturnedParcelConsumer(ISender mediator) : IConsumer<ParcelReturnedEvent>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<ParcelReturnedEvent> context) =>
        _mediator.Send(new RestockReturnedParcelCommand(context.Message.ReturnId, context.Message.OrderId, context.Message.Items),
            context.CancellationToken);
}
