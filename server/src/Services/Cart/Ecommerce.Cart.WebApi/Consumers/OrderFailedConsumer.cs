using Ecommerce.Cart.Application.Checkout;
using Ecommerce.Contracts.Order;
using MassTransit;

namespace Ecommerce.Cart.WebApi.Consumers;

/// <summary>
/// A failed order leaves the cart exactly as it was, so the customer can simply check out again.
/// </summary>
public class OrderFailedConsumer(CheckoutOutcomes outcomes) : IConsumer<OrderFailedEvent>
{
    private readonly CheckoutOutcomes _outcomes = outcomes;

    public Task Consume(ConsumeContext<OrderFailedEvent> context)
        => _outcomes.RecordFailedAsync(context.Message.OrderId, context.CancellationToken);
}
