using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Infrastructure.Catalog;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
// Aliased, not imported: the generated namespace has its own CartItem, which collides with Order's.
using PricedVariant = Ecommerce.Contracts.Grpc.PricedVariant;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Which shop each parcel comes from (specs/036): the shop's name is frozen onto the order line at
/// checkout, and the order's lines, parcels and quote carry it.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class ShopNameTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    [Fact]
    public async Task Checkout_freezes_the_shop_name_on_each_line()
    {
        var alice = Guid.CreateVersion7();
        var hers = Offer(alice, "Alice Cameras");
        var shops = Offer(null, null);

        var order = await CheckoutAsync(hers, shops);

        var lines = (await OrderSeed.ReadAsync(_fixture, order)).Items.ToDictionary(i => i.VariantId!.Value);
        Assert.Equal("Alice Cameras", lines[hers].SellerName);
        Assert.Null(lines[shops].SellerName);
    }

    /// <summary>
    /// research D1: an order is a record of who the customer bought from. Catalog answering with a new
    /// name afterwards - a rename - must not reach an order that already exists.
    /// </summary>
    [Fact]
    public async Task A_shop_renamed_afterwards_does_not_rename_an_existing_order()
    {
        var alice = Guid.CreateVersion7();
        var hers = Offer(alice, "Alice Cameras");
        var order = await CheckoutAsync(hers);

        var renamed = _fixture.Checkout.Prices[hers] with { SellerName = "Alice Photo Lab" };
        _fixture.Checkout.Prices[hers] = renamed;

        var detail = await ReadOrderAsync(order);
        Assert.Equal("Alice Cameras", Assert.Single(detail.Items).SellerName);
    }

    [Fact]
    public async Task Each_parcel_says_who_sends_it_and_which_is_the_shop_s()
    {
        var alice = Guid.CreateVersion7();
        var order = await CheckoutAsync(Offer(alice, "Alice Cameras"), Offer(null, null));

        var parcels = (await ReadOrderAsync(order)).Shipments!;

        Assert.Equal(2, parcels.Count);
        var shop = Assert.Single(parcels, p => p.IsShop);
        Assert.Null(shop.SellerName);
        Assert.Equal("Alice Cameras", Assert.Single(parcels, p => !p.IsShop).SellerName);
    }

    /// <summary>The customer sees who sells each line BEFORE paying, on the same quote that prices it.</summary>
    [Fact]
    public async Task The_checkout_quote_names_the_shop_on_each_line()
    {
        var alice = Guid.CreateVersion7();
        var hers = Offer(alice, "Alice Cameras");
        Arrange(hers);

        await using var scope = _fixture.NewScope();
        var quote = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetCheckoutQuoteQuery(null, "standard"));

        Assert.Equal("Alice Cameras", Assert.Single(quote.Items).SellerName);
    }

    /// <summary>
    /// FR-003: a name Catalog does not have is recorded as none. An empty string would print as a blank
    /// heading; the transport turns "not sent" and "empty" alike into null.
    /// </summary>
    [Fact]
    public void An_absent_or_empty_name_on_the_wire_is_no_name()
    {
        Assert.Null(GrpcCatalogPrices.SellerNameOf(new PricedVariant()));
        Assert.Null(GrpcCatalogPrices.SellerNameOf(new PricedVariant { SellerName = "" }));
        Assert.Equal("Alice Cameras", GrpcCatalogPrices.SellerNameOf(new PricedVariant { SellerName = "Alice Cameras" }));
    }

    // ------------------------------------------------------------------ helpers

    private Guid Offer(Guid? seller, string? shopName)
    {
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        _fixture.Checkout.Prices[variant] = new CatalogPrice(
            product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller, shopName);
        return variant;
    }

    private void Arrange(params Guid[] variants)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = variants
            .Select(v => new CartItem(_fixture.Checkout.Prices[v].ProductId, 1, v))
            .ToList();
        _fixture.Checkout.Address = Home;
    }

    private async Task<Guid> CheckoutAsync(params Guid[] variants)
    {
        Arrange(variants);
        await using var scope = _fixture.NewScope();
        return (await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SubmitOrderCommand(null, "standard"))).OrderId;
    }

    private async Task<Application.Orders.Common.OrderDetailResponse> ReadOrderAsync(Guid order)
    {
        _fixture.CurrentUser.Id = (await OrderSeed.ReadAsync(_fixture, order)).UserId;
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMyOrderByIdQuery(order));
    }
}
