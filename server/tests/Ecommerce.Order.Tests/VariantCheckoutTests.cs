using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What is bought is a VARIANT, and the order freezes enough words to keep describing it afterwards
/// (specs/020).
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class VariantCheckoutTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    [Fact]
    public async Task The_line_freezes_the_variant_its_sku_and_what_was_chosen()
    {
        var product = Guid.CreateVersion7();
        var kit = Guid.CreateVersion7();
        Arrange(product, kit, price: 1400m, sku: "CAM-1-KIT", options: "Kit: With 24-105mm · Colour: Black");

        var response = await CheckoutAsync();

        var line = Assert.Single(response.Items);
        Assert.Equal(kit, line.VariantId);
        Assert.Equal("CAM-1-KIT", line.Sku);
        Assert.Equal("Kit: With 24-105mm · Colour: Black", line.OptionSummary);
        Assert.Equal(product, line.ProductId);
        Assert.Equal(1400m, line.UnitPrice);

        // Stored, not just returned: the row is what a customer reads next year.
        var row = await OrderSeed.ReadAsync(_fixture, response.OrderId);
        var stored = Assert.Single(row.Items);
        Assert.Equal(kit, stored.VariantId);
        Assert.Equal("CAM-1-KIT", stored.Sku);
        Assert.Equal("Kit: With 24-105mm · Colour: Black", stored.OptionSummary);
    }

    [Fact]
    public async Task The_variant_travels_with_the_event_so_Inventory_holds_the_right_stock()
    {
        var product = Guid.CreateVersion7();
        var kit = Guid.CreateVersion7();
        Arrange(product, kit, price: 20m, sku: "K", options: "Kit: With lens");

        var response = await CheckoutAsync();

        var published = _fixture.Harness.Published
            .Select<OrderSubmittedEvent>()
            .Single(m => m.Context.Message.OrderId == response.OrderId);

        var item = Assert.Single(published.Context.Message.Items);
        Assert.Equal(kit, item.VariantId);
        Assert.Equal(product, item.ProductId);   // both: one to hold stock against, one to link to
    }

    [Fact]
    public async Task A_variant_that_cannot_be_sold_refuses_the_whole_order()
    {
        var product = Guid.CreateVersion7();
        var kit = Guid.CreateVersion7();
        Arrange(product, kit, price: 20m, sku: "K", options: "Kit: With lens", sellable: false);

        await Assert.ThrowsAsync<ConflictException>(CheckoutAsync);
    }

    [Fact]
    public async Task A_cart_line_from_before_variants_still_checks_out()
    {
        // No variant on the line, and Catalog answering as it did then: the product's id is its only
        // variant's id (specs/020 research D2), so nothing has to be migrated for this to work.
        var product = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(product, 2)];
        _fixture.Checkout.Prices[product] = new CatalogPrice(product, "Old camera", 500m, Sellable: true);
        _fixture.Checkout.Address = Home;

        var response = await CheckoutAsync();

        var line = Assert.Single(response.Items);

        // The variant recorded is the product's own id, because that IS its only variant's id. Writing
        // it down is truer than leaving it null: the line names exactly what was bought, and Inventory
        // holds stock against the same id.
        Assert.Equal(product, line.VariantId);
        Assert.Equal(product, line.ProductId);
        Assert.Null(line.Sku);              // a catalogue answering as it did then has no sku to give
        Assert.Null(line.OptionSummary);    // and nothing to choose between
        Assert.Equal(1000m, line.TotalPrice);
    }

    private void Arrange(Guid product, Guid variant, decimal price, string sku, string options, bool sellable = true)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(product, 1, variant)];
        _fixture.Checkout.Prices[variant] = new CatalogPrice(product, "Camera", price, sellable, variant, sku, options);
        _fixture.Checkout.Address = Home;
    }

    private async Task<OrderResponse> CheckoutAsync()
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SubmitOrderCommand(null, "standard"));
    }
}
