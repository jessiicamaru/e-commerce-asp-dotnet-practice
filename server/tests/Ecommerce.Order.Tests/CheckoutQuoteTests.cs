using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// The storefront shows what checkout will cost before the customer commits (#38). The quote is only
/// worth showing if it is the number then charged.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class CheckoutQuoteTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private readonly Guid _widget = Guid.CreateVersion7();
    private readonly Guid _gadget = Guid.CreateVersion7();

    private static readonly AddressCopy London =
        new("Jane Doe", "10 Downing St", null, "London", null, "SW1A 2AA", "GB", null);

    private void ArrangeCart()
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_widget, 3), new CartItem(_gadget, 1)];
        _fixture.Checkout.Prices[_widget] = new CatalogPrice(_widget, "Widget", 9.99m, Sellable: true);
        _fixture.Checkout.Prices[_gadget] = new CatalogPrice(_gadget, "Gadget", 0.05m, Sellable: true);
        _fixture.Checkout.Address = London;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<int> OrdersOfCurrentUserAsync()
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        return await context.Orders.CountAsync(o => o.UserId == _fixture.CurrentUser.Id);
    }

    [Fact]
    public async Task The_quote_is_exactly_what_the_same_choices_then_charge()
    {
        ArrangeCart();

        var quote = await SendAsync(new GetCheckoutQuoteQuery(null, "standard"));
        var order = await SendAsync(new SubmitOrderCommand(null, "standard"));

        // 3 x 9.99 + 1 x 0.05 = 30.02; delivery 5.00; GB 20%: 5.99 (5.994) + 0.01 + 1.00 = 7.00.
        Assert.Equal(30.02m, quote.Subtotal);
        Assert.Equal(5.00m, quote.ShippingPrice);
        Assert.Equal(7.00m, quote.TaxTotal);
        Assert.Equal(0.20m, quote.TaxRate);
        Assert.Equal(42.02m, quote.TotalAmount);

        Assert.Equal(quote.Subtotal, order.Subtotal);
        Assert.Equal(quote.ShippingPrice, order.ShippingPrice);
        Assert.Equal(quote.TaxTotal, order.TaxTotal);
        Assert.Equal(quote.DiscountTotal, order.DiscountTotal);
        Assert.Equal(quote.TaxRate, order.TaxRate);
        Assert.Equal(quote.TotalAmount, order.TotalAmount);
        Assert.Equal(
            quote.Items.Select(i => (i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.TaxAmount)),
            order.Items.Select(i => (i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.TaxAmount)));

        // And what is stored - the number the saga charges - is the same one.
        var row = await OrderSeed.ReadAsync(_fixture, order.OrderId);
        Assert.Equal(quote.TotalAmount, row.TotalAmount);
    }

    [Fact]
    public async Task A_quote_places_nothing_and_publishes_nothing()
    {
        ArrangeCart();
        var publishedBefore = _fixture.Harness.Published.Select<OrderSubmittedEvent>().Count();

        var quote = await SendAsync(new GetCheckoutQuoteQuery(null, "express"));

        Assert.Equal("express", quote.ShippingOption.Code);
        Assert.Equal("GB", quote.ShippingAddress.Country);
        Assert.Equal(0, await OrdersOfCurrentUserAsync());
        Assert.Equal(publishedBefore, _fixture.Harness.Published.Select<OrderSubmittedEvent>().Count());
    }

    [Fact]
    public async Task An_empty_cart_is_refused_as_checkout_would_refuse_it()
    {
        ArrangeCart();
        _fixture.Checkout.Cart = [];

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new GetCheckoutQuoteQuery(null, "standard")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("teleport")]
    public async Task An_unknown_delivery_option_is_refused_as_checkout_would_refuse_it(string option)
    {
        ArrangeCart();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => SendAsync(new GetCheckoutQuoteQuery(null, option)));
    }
}
