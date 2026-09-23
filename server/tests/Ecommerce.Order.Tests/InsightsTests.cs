using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Insights;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// The admin's insights (specs/047): revenue is the sum of paid orders that were not cancelled, per
/// currency and never across currencies; what sold and who bought come from the same orders.
/// </summary>
/// <remarks>
/// Each test moves its orders into a day of its own far in the future, so the rest of the collection's
/// orders - which are placed "now" - are never inside the period being asked about.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class InsightsTests(OrderTestFixture fixture)
{
    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task Revenue_counts_paid_orders_per_currency_and_nothing_else()
    {
        var day = Day();
        var buyer = Guid.CreateVersion7();
        var paid = await PlaceAsync(buyer, "VND", 1_000_000m, OrderStatus.Paid, day);
        var shipped = await PlaceAsync(buyer, "VND", 3_000_000m, OrderStatus.Shipped, day);
        var preparing = await PlaceAsync(buyer, "USD", 100m, OrderStatus.Preparing, day.AddDays(1));
        await PlaceAsync(buyer, "VND", 9_000_000m, OrderStatus.Cancelled, day);     // not revenue
        await PlaceAsync(buyer, "VND", 9_000_000m, OrderStatus.Failed, day);        // not revenue
        await PlaceAsync(buyer, "VND", 9_000_000m, OrderStatus.Submitted, day);     // not yet

        var revenue = await SendAsync(new GetRevenueQuery(day, day.AddDays(2)));

        var dong = Assert.Single(revenue.Totals, t => t.Currency == "VND");
        var dollars = Assert.Single(revenue.Totals, t => t.Currency == "USD");
        Assert.Equal(2, dong.Orders);
        Assert.Equal(await TotalOfAsync(paid) + await TotalOfAsync(shipped), dong.Revenue);
        Assert.Equal(dong.Revenue / 2, dong.AverageOrderValue);
        Assert.Equal((1, await TotalOfAsync(preparing)), (dollars.Orders, dollars.Revenue));
        Assert.Equal(2, revenue.Days.Count);   // one row per currency per day: VND on day one, USD on day two
        Assert.Equal(dong.Revenue, revenue.Days.Where(d => d.Currency == "VND").Sum(d => d.Revenue));
    }

    [Fact]
    public async Task Top_products_count_units_and_keep_revenue_per_currency()
    {
        var day = Day();
        var lens = Guid.CreateVersion7();
        var body = Guid.CreateVersion7();
        await PlaceAsync(Guid.CreateVersion7(), "VND", 1_000m, OrderStatus.Paid, day, (lens, 3), (body, 1));
        await PlaceAsync(Guid.CreateVersion7(), "USD", 10m, OrderStatus.Paid, day, (lens, 1));
        await PlaceAsync(Guid.CreateVersion7(), "VND", 1_000m, OrderStatus.Cancelled, day, (body, 50));

        var top = await SendAsync(new GetTopProductsQuery(day, day.AddDays(1)));

        Assert.Equal([lens, body], top.Select(p => p.ProductId));
        Assert.Equal(4, top[0].Units);
        Assert.Equal(["VND", "USD"], top[0].Revenue.Select(r => r.Currency).Order().Reverse());
    }

    [Fact]
    public async Task Top_buyers_rank_by_spend_in_the_asked_currency()
    {
        var day = Day();
        var small = Guid.CreateVersion7();
        var big = Guid.CreateVersion7();
        await PlaceAsync(small, "VND", 1_000m, OrderStatus.Paid, day);
        await PlaceAsync(small, "VND", 1_000m, OrderStatus.Paid, day);
        await PlaceAsync(big, "VND", 50_000m, OrderStatus.Paid, day);

        var top = await SendAsync(new GetTopBuyersQuery(day, day.AddDays(1), "VND"));

        Assert.Equal([big, small], top.Select(b => b.CustomerId));
        Assert.Equal(2, top[1].Orders);
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime Day() =>
        new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(0, 3000));

    /// <summary>Places an order in this currency, puts it in this state, and moves it to this day.</summary>
    private async Task<Guid> PlaceAsync(Guid buyer, string currency, decimal price, OrderStatus status, DateTime day,
        params (Guid Product, int Quantity)[] lines)
    {
        if (lines.Length == 0) lines = [(Guid.CreateVersion7(), 1)];
        _fixture.CurrentUser.Id = buyer;
        _fixture.Currency = new Currency(currency, currency == "VND" ? 0 : 2);
        var cart = new List<CartItem>();
        foreach (var (product, quantity) in lines)
        {
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, $"Camera {product:N}"[..14], price, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", currency, null, null);
            cart.Add(new CartItem(product, quantity, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        var order = (await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId;

        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Orders.Where(o => o.Id == order)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, status).SetProperty(o => o.CreatedAt, day.AddHours(12)));
        return order;
    }

    private async Task<decimal> TotalOfAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders
            .Where(o => o.Id == order).Select(o => o.TotalAmount).SingleAsync();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
