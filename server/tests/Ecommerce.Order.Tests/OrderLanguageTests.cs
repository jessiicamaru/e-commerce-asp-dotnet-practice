using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// An order keeps the words it was bought with, in the language it was bought in (specs/021).
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class OrderLanguageTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    [Fact]
    public async Task Checkout_asks_the_catalogue_in_the_language_of_the_request()
    {
        var variant = Arrange(language: "vi", name: "Máy ảnh Sony A7 IV", options: "Bộ: Chỉ thân máy");

        var response = await CheckoutAsync();

        // Catalog was asked in Vietnamese, so the words it answered with - and the order froze - are
        // Vietnamese. Ask in English tomorrow and this order still says what it said today.
        Assert.Equal("vi", _fixture.Checkout.LastLanguageAsked);

        var line = Assert.Single(response.Items);
        Assert.Equal("Máy ảnh Sony A7 IV", line.ProductName);
        Assert.Equal("Bộ: Chỉ thân máy", line.OptionSummary);

        var row = await OrderSeed.ReadAsync(_fixture, response.OrderId);
        Assert.Equal("vi", row.Language);
        Assert.Equal("Máy ảnh Sony A7 IV", Assert.Single(row.Items).ProductName);
        Assert.Equal(variant, Assert.Single(row.Items).VariantId);
    }

    [Fact]
    public async Task An_order_placed_in_english_keeps_english_words()
    {
        Arrange(language: "en", name: "Sony A7 IV", options: "Kit: Body only");

        var response = await CheckoutAsync();

        Assert.Equal("en", _fixture.Checkout.LastLanguageAsked);
        Assert.Equal("Sony A7 IV", Assert.Single(response.Items).ProductName);
        Assert.Equal("en", (await OrderSeed.ReadAsync(_fixture, response.OrderId)).Language);
    }

    private Guid Arrange(string language, string name, string options)
    {
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();

        _fixture.Language = language;
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(product, 1, variant)];
        _fixture.Checkout.Prices[variant] = new CatalogPrice(product, name, 100m, true, variant, "SKU-1", options);
        _fixture.Checkout.Address = Home;

        return variant;
    }

    private async Task<OrderResponse> CheckoutAsync()
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SubmitOrderCommand(null, "standard"));
    }
}
