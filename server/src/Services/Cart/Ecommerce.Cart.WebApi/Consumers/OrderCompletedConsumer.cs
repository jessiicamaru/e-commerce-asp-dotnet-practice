using Ecommerce.Cart.Application.Checkout;
using Ecommerce.Contracts.Order;
using MassTransit;

namespace Ecommerce.Cart.WebApi.Consumers;

/// <summary>
/// Removes what the order bought from the cart.
/// </summary>
/// <remarks>
/// <b>The third service with a consumer of this name.</b> Inventory and Order each have one. Without
/// the "CartSvc" endpoint prefix all three would bind one queue and compete, so each would see the
/// event only sometimes - which is the exact defect that once settled an order while its stock stayed
/// held, with every unit test green.
/// </remarks>
public class OrderCompletedConsumer(CheckoutOutcomes outcomes) : IConsumer<OrderCompletedEvent>
{
    private readonly CheckoutOutcomes _outcomes = outcomes;

    public Task Consume(ConsumeContext<OrderCompletedEvent> context)
        => _outcomes.RecordCompletedAsync(context.Message.OrderId, context.CancellationToken);
}
