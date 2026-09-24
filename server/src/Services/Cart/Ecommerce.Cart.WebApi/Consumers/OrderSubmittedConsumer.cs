using Ecommerce.Cart.Application.Checkout;
using Ecommerce.Contracts.Order;
using MassTransit;

namespace Ecommerce.Cart.WebApi.Consumers;

/// <summary>
/// Remembers what an order will remove from the cart, if it completes.
/// </summary>
/// <remarks>
/// Class name shared with nothing else by virtue of the "CartSvc" endpoint prefix in Program.cs. A
/// consumer's class name becomes its queue name, and two services sharing one compete for a single
/// copy of the event.
/// </remarks>
public class OrderSubmittedConsumer(CheckoutOutcomes outcomes) : IConsumer<OrderSubmittedEvent>
{
    private readonly CheckoutOutcomes _outcomes = outcomes;

    public Task Consume(ConsumeContext<OrderSubmittedEvent> context)
    {
        var m = context.Message;

        return _outcomes.RecordSubmittedAsync(
            m.OrderId,
            m.UserId,
            (m.Items ?? []).Select(OrderedItem.From).ToList(),
            context.CancellationToken);
    }
}
