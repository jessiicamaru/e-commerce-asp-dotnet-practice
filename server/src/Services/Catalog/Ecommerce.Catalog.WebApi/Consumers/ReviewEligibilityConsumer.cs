using Ecommerce.Catalog.Application.Reviews;
using Ecommerce.Contracts.Order;
using MassTransit;
using MediatR;

namespace Ecommerce.Catalog.WebApi.Consumers;

/// <summary>
/// A parcel reached its customer (specs/040), so they may review what was in it (specs/046). The class name
/// is the queue name: nothing else consumes this event yet, and Catalog's endpoint prefix keeps it apart
/// from any future consumer of the same name elsewhere.
/// </summary>
public class ReviewEligibilityConsumer(ISender sender) : IConsumer<ParcelDeliveredEvent>
{
    private readonly ISender _sender = sender;

    public Task Consume(ConsumeContext<ParcelDeliveredEvent> context) =>
        _sender.Send(new RecordReviewEligibilityCommand(
            context.Message.BuyerId, context.Message.ProductIds, context.Message.DeliveredAt), context.CancellationToken);
}
