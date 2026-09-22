using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Orders.Queries.GetShippingOptions;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// An order is placed in one currency, freezes it, and every amount on it is in that currency
/// (specs/022).
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class OrderCurrencyTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;
    private readonly Guid _camera = Guid.CreateVersion7();

    private static readonly Currency Dong = new("VND", 0);
    private static readonly Currency Dollars = new("USD", 2);

    private static readonly AddressCopy Hanoi =
        new("Nguyen Van A", "1 Trang Tien", null, "Ha Noi", null, "100000", "VN", null);

    [Fact]
    public async Task The_order_freezes_the_currency_it_was_placed_in()
    {
        ArrangeCart(Dollars, unitPrice: 1499m);

        var response = await CheckoutAsync();

        Assert.Equal("USD", response.Currency);

        // ...and reading it back later says the same, whatever the reader is browsing in.
        _fixture.Currency = Dong;
        var detail = await SendAsync(new GetMyOrderByIdQuery(response.OrderId));

        Assert.Equal("USD", detail!.Currency);
    }

    [Fact]
    public async Task Checkout_asks_Catalog_to_price_in_the_currency_of_the_request()
    {
        ArrangeCart(Dollars, unitPrice: 1499m);

        await CheckoutAsync();

        // The words AND the money. Sending one and not the other is what shipped in specs/021.
        Assert.Equal("USD", _fixture.Checkout.LastCurrencyAsked);
        Assert.Equal("vi", _fixture.Checkout.LastLanguageAsked);
    }

    [Fact]
    public async Task A_dong_total_has_no_fractional_part_and_the_parts_still_sum()
    {
        // 10% of 3 x 12,345,678 is 3,703,703.4 - a number with a fraction of a dong in it, which is
        // not a thing. Rounding to two decimals, as every amount was rounded before specs/022, would
        // store 3,703,703.40.
        ArrangeCart(Dong, unitPrice: 12_345_678m);

        var response = await CheckoutAsync();
        var row = await OrderSeed.ReadAsync(_fixture, response.OrderId);

        Assert.Equal(decimal.Truncate(row.TaxTotal!.Value), row.TaxTotal);
        Assert.Equal(decimal.Truncate(row.TotalAmount), row.TotalAmount);
        Assert.Equal(decimal.Truncate(row.ShippingPrice!.Value), row.ShippingPrice);

        // The CHECK constraint is what refuses a row whose parts do not add up; this asserts the
        // arithmetic that satisfied it.
        Assert.Equal(row.TotalAmount, row.Subtotal + row.ShippingPrice + row.TaxTotal - row.DiscountTotal);
    }

    [Fact]
    public async Task The_same_cart_in_two_currencies_is_charged_two_unrelated_amounts()
    {
        ArrangeCart(Dong, unitPrice: 40_000_000m);
        var inDong = await QuoteAsync();

        // A price somebody DECIDED, not one a rate produced: 1,499 is not 40,000,000 over anything.
        ArrangeCart(Dollars, unitPrice: 1499m);
        var inDollars = await QuoteAsync();

        Assert.Equal("VND", inDong.Currency);
        Assert.Equal("USD", inDollars.Currency);

        // Delivery differs too, and not by the same factor as the goods - each list is set by hand.
        Assert.Equal(30_000m, inDong.ShippingPrice);
        Assert.Equal(5.00m, inDollars.ShippingPrice);
    }

    [Fact]
    public async Task A_variant_not_priced_in_the_currency_refuses_the_checkout_and_names_it()
    {
        _fixture.Currency = Dollars;
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_camera, 1)];
        _fixture.Checkout.Address = Hanoi;

        // What Catalog answers for a variant nobody has priced in dollars: no price, not sellable.
        _fixture.Checkout.Prices[_camera] =
            new CatalogPrice(_camera, "Sony A7 IV", null, Sellable: false, Currency: "USD");

        var refusal = await Assert.ThrowsAsync<ConflictException>(CheckoutAsync);

        // "Not sold in USD" rather than "not for sale": the second sends a customer looking for a
        // product that is on sale in dong, and an administrator looking for a withdrawal that never
        // happened.
        Assert.Contains("USD", refusal.Message);
        Assert.Contains("Sony A7 IV", refusal.Message);
    }

    [Fact]
    public async Task A_delivery_option_not_priced_in_the_currency_is_not_offered()
    {
        _fixture.Currency = Dollars;

        var inDollars = await SendAsync(new GetShippingOptionsQuery());

        // "overnight" is priced in dong only.
        Assert.DoesNotContain(inDollars, o => o.Code == "overnight");
        Assert.All(inDollars, o => Assert.Equal("USD", o.Currency));
        Assert.All(inDollars, o => Assert.NotNull(o.Price));

        _fixture.Currency = Dong;
        Assert.Contains(await SendAsync(new GetShippingOptionsQuery()), o => o.Code == "overnight");
    }

    [Fact]
    public async Task Choosing_a_delivery_option_that_is_not_offered_in_the_currency_is_refused()
    {
        ArrangeCart(Dollars, unitPrice: 1499m);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new SubmitOrderCommand(null, "overnight")));
    }

    [Fact]
    public async Task The_currency_travels_with_the_amount_to_the_saga()
    {
        ArrangeCart(Dollars, unitPrice: 1499m);

        var response = await CheckoutAsync();

        var published = _fixture.Harness.Published
            .Select<OrderSubmittedEvent>()
            .Single(m => m.Context.Message.OrderId == response.OrderId);

        // The relay the Orchestrator republishes into ProcessPaymentCommand. Without this field a
        // payment row records an amount and nothing that says what of - which is the defect
        // specs/022 exists to end, and it cannot be detected downstream.
        Assert.Equal("USD", published.Context.Message.Currency);
        Assert.Equal(response.TotalAmount, published.Context.Message.TotalAmount);
    }

    [Fact]
    public async Task The_quote_and_the_order_agree_on_the_currency_as_well_as_the_number()
    {
        ArrangeCart(Dollars, unitPrice: 1499m);

        var quote = await QuoteAsync();
        var order = await CheckoutAsync();

        Assert.Equal(quote.Currency, order.Currency);
        Assert.Equal(quote.TotalAmount, order.TotalAmount);
    }

    private void ArrangeCart(Currency currency, decimal unitPrice)
    {
        _fixture.Currency = currency;

        // Stated, not inherited. Both holders are singletons for the whole collection, so a test that
        // relies on whatever the previous one left passes or fails by running order.
        _fixture.Language = "vi";
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_camera, 3)];
        _fixture.Checkout.Prices[_camera] =
            new CatalogPrice(_camera, "Sony A7 IV", unitPrice, Sellable: true, Currency: currency.Code);
        _fixture.Checkout.Address = Hanoi;
    }

    private Task<OrderResponse> CheckoutAsync() => SendAsync(new SubmitOrderCommand(null, "standard"));

    private Task<CheckoutQuoteResponse> QuoteAsync() =>
        SendAsync(new GetCheckoutQuoteQuery(null, "standard"));

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
