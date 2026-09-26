using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Insights;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A seller's own insights (specs/068, #111): revenue is the sum of THEIR lines - unit price × quantity,
/// before tax - on sold orders, per currency; never the order's total, never another seller's lines, and
/// never a parcel that came back and was refunded.
/// </summary>
/// <remarks>
/// Every test mints its own sellers and moves its orders to a day of its own far in the future, as
/// <see cref="InsightsTests"/> does, so nothing else the collection places is inside the period.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class SellerInsightsTests(OrderTestFixture fixture)
{
    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task Revenue_is_the_sum_of_the_sellers_own_lines_before_tax_never_the_orders_total()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        var bao = Guid.CreateVersion7();
        // One order, two sellers and the shop: Mai's line is 2 × 1,000; the order's total holds far more.
        var shared = await PlaceAsync("VND", OrderStatus.Paid, day, (mai, 1_000m, 2), (bao, 50_000m, 1), (null, 70_000m, 1));

        var revenue = await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day));

        var dong = Assert.Single(revenue.Totals);
        Assert.Equal(("VND", 2_000m, 1), (dong.Currency, dong.Revenue, dong.Orders));
        Assert.True(await TotalOfAsync(shared) > dong.Revenue);
        Assert.Equal(2_000m, Assert.Single(revenue.Days).Revenue);
    }

    [Fact]
    public async Task Another_sellers_lines_never_appear()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        var bao = Guid.CreateVersion7();
        await PlaceAsync("VND", OrderStatus.Paid, day, (bao, 9_000m, 3));

        var revenue = await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day));
        var top = await AsSellerAsync(mai, new GetSellerTopProductsQuery(day, day));

        Assert.Empty(revenue.Totals);
        Assert.Empty(revenue.Days);
        Assert.Empty(top);
    }

    /// <summary>The Overview's one definition of a sale (specs/047): not failed, cancelled or still settling.</summary>
    [Fact]
    public async Task Only_sold_orders_count()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        foreach (var sold in new[] { OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Shipped })
            await PlaceAsync("VND", sold, day, (mai, 1_000m, 1));
        foreach (var notSold in new[] { OrderStatus.Cancelled, OrderStatus.Failed, OrderStatus.Submitted })
            await PlaceAsync("VND", notSold, day, (mai, 100_000m, 1));

        var dong = Assert.Single((await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day))).Totals);

        Assert.Equal((3_000m, 3), (dong.Revenue, dong.Orders));
    }

    /// <summary>
    /// Research D1: a parcel whose return was RECEIVED was refunded and earns the seller nothing (specs/066), so
    /// it is not revenue either. A return still open is - it may yet be refused.
    /// </summary>
    [Fact]
    public async Task A_parcel_returned_and_refunded_is_not_revenue_and_one_still_open_is()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        var kept = await PlaceAsync("VND", OrderStatus.Shipped, day, (mai, 1_000m, 1));
        var returned = await PlaceAsync("VND", OrderStatus.Shipped, day, (mai, 20_000m, 1));
        var open = await PlaceAsync("VND", OrderStatus.Shipped, day, (mai, 300_000m, 1));
        await ReturnAsync(returned, mai, ReturnStatus.Received);
        await ReturnAsync(open, mai, ReturnStatus.Requested);

        var dong = Assert.Single((await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day))).Totals);
        var top = await AsSellerAsync(mai, new GetSellerTopProductsQuery(day, day));

        Assert.Equal((301_000m, 2), (dong.Revenue, dong.Orders));
        Assert.Equal(2, top.Count);
        Assert.NotNull(kept);
    }

    /// <summary>Only Mai's part came back: Bao's line on the same order is still his revenue.</summary>
    [Fact]
    public async Task A_return_is_one_sellers_part_not_the_whole_order()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        var bao = Guid.CreateVersion7();
        var order = await PlaceAsync("VND", OrderStatus.Shipped, day, (mai, 1_000m, 1), (bao, 5_000m, 1));
        await ReturnAsync(order, mai, ReturnStatus.Received);

        Assert.Empty((await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day))).Totals);
        Assert.Equal(5_000m, Assert.Single((await AsSellerAsync(bao, new GetSellerRevenueQuery(day, day))).Totals).Revenue);
    }

    /// <summary>Three of her lines on one order are one order, not three.</summary>
    [Fact]
    public async Task An_order_is_counted_once_however_many_of_the_sellers_lines_it_holds()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        await PlaceAsync("VND", OrderStatus.Paid, day, (mai, 1_000m, 1), (mai, 2_000m, 1), (mai, 3_000m, 1));
        await PlaceAsync("VND", OrderStatus.Paid, day, (mai, 4_000m, 1));

        var dong = Assert.Single((await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day))).Totals);

        Assert.Equal((10_000m, 2, 5_000m), (dong.Revenue, dong.Orders, dong.AverageOrderValue));
        Assert.Equal(2, Assert.Single((await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day))).Days).Orders);
    }

    [Fact]
    public async Task Money_is_never_added_across_currencies()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        await PlaceAsync("VND", OrderStatus.Paid, day, (mai, 1_000_000m, 1));
        await PlaceAsync("USD", OrderStatus.Paid, day.AddDays(1), (mai, 40m, 1));

        var revenue = await AsSellerAsync(mai, new GetSellerRevenueQuery(day, day.AddDays(1)));

        Assert.Equal([("VND", 1_000_000m), ("USD", 40m)], revenue.Totals.Select(t => (t.Currency, t.Revenue)));
        Assert.Equal(2, revenue.Days.Count);
    }

    /// <summary>The one period rule (specs/055): whole UTC days, both ends included.</summary>
    [Fact]
    public async Task A_period_is_whole_days_both_ends_included()
    {
        var first = Day();
        var mai = Guid.CreateVersion7();
        var early = await PlaceAsync("VND", OrderStatus.Paid, first, (mai, 1_000m, 1));
        var late = await PlaceAsync("VND", OrderStatus.Paid, first.AddDays(1), (mai, 2_000m, 1));
        var before = await PlaceAsync("VND", OrderStatus.Paid, first, (mai, 4_000m, 1));
        await MoveAsync(early, first.AddMinutes(1));
        await MoveAsync(late, first.AddDays(2).AddMinutes(-1));
        await MoveAsync(before, first.AddMinutes(-1));

        var revenue = await AsSellerAsync(mai, new GetSellerRevenueQuery(first.AddHours(15), first.AddDays(1).AddHours(3)));

        Assert.Equal(3_000m, Assert.Single(revenue.Totals).Revenue);
        Assert.Equal((first, first.AddDays(2)), (revenue.From, revenue.To));
    }

    [Fact]
    public async Task The_period_rule_refuses_what_every_insight_refuses()
    {
        var to = Day();
        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            AsSellerAsync(Guid.CreateVersion7(), new GetSellerRevenueQuery(to.AddDays(-366), to)));
        Assert.Contains("at most 366 days", refused.Message);

        refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            AsSellerAsync(Guid.CreateVersion7(), new GetSellerTopProductsQuery(to.AddDays(1), to)));
        Assert.Contains("must not start after it ends", refused.Message);
    }

    [Fact]
    public async Task Top_products_rank_the_sellers_own_by_units_with_revenue_per_currency()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        var lens = Guid.CreateVersion7();
        var body = Guid.CreateVersion7();
        await PlaceAsync("VND", OrderStatus.Paid, day, (mai, 1_000m, 1, body), (mai, 500m, 3, lens));
        await PlaceAsync("USD", OrderStatus.Paid, day, (mai, 10m, 2, lens));
        await PlaceAsync("VND", OrderStatus.Paid, day, (Guid.CreateVersion7(), 1m, 90, lens));   // someone else's units

        var top = await AsSellerAsync(mai, new GetSellerTopProductsQuery(day, day));

        Assert.Equal([lens, body], top.Select(p => p.ProductId));
        Assert.Equal(5, top[0].Units);
        Assert.Equal([("VND", 1_500m), ("USD", 20m)], top[0].Revenue.Select(r => (r.Currency, r.Amount)).OrderByDescending(r => r.Amount));
    }

    /// <summary>A token with no user id is refused rather than read as "the shop's own" (a null seller).</summary>
    [Fact]
    public async Task A_caller_without_an_id_is_refused()
    {
        _fixture.CurrentUser.Id = null;
        await using var scope = _fixture.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetSellerRevenueQuery()));
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime Day() =>
        new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(0, 3000));

    private async Task<T> AsSellerAsync<T>(Guid seller, IRequest<T> request)
    {
        _fixture.CurrentUser.Id = seller;
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private Task<Guid> PlaceAsync(string currency, OrderStatus status, DateTime day, params (Guid? Seller, decimal Price, int Quantity)[] lines) =>
        PlaceAsync(currency, status, day, lines.Select(l => (l.Seller, l.Price, l.Quantity, Guid.CreateVersion7())).ToArray());

    /// <summary>Places an order of these lines - whose, at what price, how many of which product - then sets its state and day.</summary>
    private async Task<Guid> PlaceAsync(string currency, OrderStatus status, DateTime day,
        params (Guid? Seller, decimal Price, int Quantity, Guid Product)[] lines)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Currency = new Currency(currency, currency == "VND" ? 0 : 2);
        var cart = new List<CartItem>();
        foreach (var (seller, price, quantity, product) in lines)
        {
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, $"Camera {product:N}"[..14], price, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", currency, seller,
                seller is null ? null : "Mai's cameras");
            cart.Add(new CartItem(product, quantity, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        await using var submit = _fixture.NewScope();
        var order = (await submit.ServiceProvider.GetRequiredService<ISender>().Send(new SubmitOrderCommand(null, "standard"))).OrderId;

        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == order)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, status).SetProperty(o => o.CreatedAt, day.AddHours(12)));
        return order;
    }

    private async Task MoveAsync(Guid order, DateTime createdAt)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == order)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.CreatedAt, createdAt));
    }

    /// <summary>Gives the seller's part of this order a return in this state, as the return flow would have left it.</summary>
    private async Task ReturnAsync(Guid order, Guid seller, ReturnStatus status)
    {
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var part = await db.OrderShipments.SingleAsync(s => s.OrderId == order && s.SellerId == seller);
        db.ParcelReturns.Add(new ParcelReturn
        {
            Id = Guid.CreateVersion7(), OrderId = order, ShipmentId = part.Id, CustomerId = Guid.CreateVersion7(), SellerId = seller,
            Status = status, Reason = "Broken", RequestedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            ReceivedAt = status == ReturnStatus.Received ? DateTime.UtcNow : null,
        });
        await db.SaveChangesAsync();
    }

    private async Task<decimal> TotalOfAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders
            .Where(o => o.Id == order).Select(o => o.TotalAmount).SingleAsync();
    }
}
