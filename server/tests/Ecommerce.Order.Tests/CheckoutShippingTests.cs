using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Checkout sends the order somewhere and charges for sending it (feature 011).
/// </summary>
/// <remarks>
/// Cart, Catalog and Identity are faked here - they are other services. What these prove is what Order
/// does with their answers: that it freezes a copy, charges delivery, and refuses without leaving a row.
/// The tests share one fake, so they run sequentially within the collection.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class CheckoutShippingTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private readonly Guid _product = Guid.CreateVersion7();

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private void ArrangeCart(AddressCopy? address)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_product, 3)];
        _fixture.Checkout.Prices[_product] = new CatalogPrice(_product, "Widget", 9.99m, Sellable: true);
        _fixture.Checkout.Address = address;
    }

    private async Task<OrderResponse> CheckoutAsync(Guid? addressId, string option)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SubmitOrderCommand(addressId, option));
    }

    private async Task<int> OrdersOfCurrentUserAsync()
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        return await context.Orders.CountAsync(o => o.UserId == _fixture.CurrentUser.Id);
    }

    [Fact]
    public async Task Express_checkout_freezes_the_address_and_option_and_charges_items_plus_delivery()
    {
        ArrangeCart(Home);

        var response = await CheckoutAsync(null, "EXPRESS");   // codes are case-insensitive

        // 3 x 9.99 = 29.97, + 15.00 express, + 10% VN tax: 3.00 on the line (2.997 rounded) and 1.50
        // on delivery (feature 012) = 49.47.
        Assert.Equal(49.47m, response.TotalAmount);
        Assert.Equal(15.00m, response.ShippingPrice);
        Assert.Equal("express", response.ShippingOption!.Code);

        var row = await OrderSeed.ReadAsync(_fixture, response.OrderId);
        Assert.Equal("Nguyen Van A", row.ShipTo!.RecipientName);
        Assert.Equal("100000", row.ShipTo.PostalCode);
        Assert.Equal("VN", row.ShipTo.Country);
        Assert.Equal("Express delivery", row.ShippingOptionName);
        Assert.Equal(15.00m, row.ShippingPrice);
        Assert.Equal(49.47m, row.TotalAmount);

        // The number the saga charges is the one in the event - it must include delivery too.
        var published = _fixture.Harness.Published
            .Select<OrderSubmittedEvent>()
            .Single(m => m.Context.Message.OrderId == response.OrderId);
        Assert.Equal(49.47m, published.Context.Message.TotalAmount);
    }

    [Fact]
    public async Task No_address_chosen_means_the_default_is_asked_for()
    {
        ArrangeCart(Home);

        await CheckoutAsync(null, "standard");

        Assert.Null(_fixture.Checkout.LastAddressIdAsked);
    }

    [Fact]
    public async Task A_named_address_that_Identity_does_not_find_is_404_and_places_nothing()
    {
        ArrangeCart(address: null);   // missing, or somebody else's - Identity does not say which

        await Assert.ThrowsAsync<NotFoundException>(() => CheckoutAsync(Guid.CreateVersion7(), "standard"));

        Assert.Equal(0, await OrdersOfCurrentUserAsync());
    }

    [Fact]
    public async Task No_address_and_no_default_is_409_and_places_nothing()
    {
        ArrangeCart(address: null);

        await Assert.ThrowsAsync<ConflictException>(() => CheckoutAsync(null, "standard"));

        Assert.Equal(0, await OrdersOfCurrentUserAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("teleport")]
    public async Task An_unknown_or_missing_delivery_option_is_refused_before_anything_is_placed(string option)
    {
        ArrangeCart(Home);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => CheckoutAsync(null, option));

        Assert.Equal(0, await OrdersOfCurrentUserAsync());
    }
}
