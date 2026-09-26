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
/// Revenue belongs to the day an order was PAID (specs/072, #116), not the day it was placed: an order placed at
/// 23:30 and settled by the saga at 00:30 counts on the second day - for the administrator and for its seller.
/// An order with no payment time recorded (from before this) keeps counting on the day it was placed.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class RevenueDayTests(OrderTestFixture fixture)
{
    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task An_order_placed_one_day_and_paid_the_next_is_revenue_on_the_second_day()
    {
        var day = Day();
        var mai = Guid.CreateVersion7();
        var order = await PlaceAsync(mai, day.AddHours(23).AddMinutes(30));
        await SettleAsync(order, OrderStatus.Paid, day.AddDays(1).AddMinutes(30));

        var first = await AsAsync(Guid.CreateVersion7(), new GetRevenueQuery(day, day));
        var second = await AsAsync(Guid.CreateVersion7(), new GetRevenueQuery(day.AddDays(1), day.AddDays(1)));
        var hersFirst = await AsAsync(mai, new GetSellerRevenueQuery(day, day));
        var hersSecond = await AsAsync(mai, new GetSellerRevenueQuery(day.AddDays(1), day.AddDays(1)));

        Assert.Empty(first.Totals);
        Assert.Equal(DateOnly.FromDateTime(day.AddDays(1)), Assert.Single(second.Days).Day);
        Assert.Empty(hersFirst.Totals);
        Assert.Equal(1_000m, Assert.Single(hersSecond.Totals).Revenue);
        Assert.Equal(DateOnly.FromDateTime(day.AddDays(1)), Assert.Single(hersSecond.Days).Day);
        Assert.Contains(await AsAsync(Guid.CreateVersion7(), new GetTopProductsQuery(day.AddDays(1), day.AddDays(1))),
            p => p.Units == 1 && p.Revenue.Single().Amount == 1_000m);
    }

    /// <summary>An order from before specs/072 has no payment time; it keeps its old day rather than vanishing.</summary>
    [Fact]
    public async Task An_order_with_no_payment_time_counts_on_the_day_it_was_placed()
    {
        var day = Day();
        var order = await PlaceAsync(Guid.CreateVersion7(), day.AddHours(10));
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == order)
                .ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, OrderStatus.Paid));   // as an older image left it
        }

        var revenue = await AsAsync(Guid.CreateVersion7(), new GetRevenueQuery(day, day));

        Assert.Equal(DateOnly.FromDateTime(day), Assert.Single(revenue.Days).Day);
    }

    [Fact]
    public async Task The_payment_time_is_written_once_by_the_settlement_and_never_for_a_failure()
    {
        var day = Day();
        var paid = await PlaceAsync(Guid.CreateVersion7(), day);
        var failed = await PlaceAsync(Guid.CreateVersion7(), day);

        await SettleAsync(paid, OrderStatus.Paid, day.AddHours(1));
        await SettleAsync(paid, OrderStatus.Paid, day.AddHours(5), expected: 0);   // a redelivery moves nothing
        await SettleAsync(failed, OrderStatus.Failed, day.AddHours(1));

        Assert.Equal(day.AddHours(1), await PaidAtAsync(paid));
        Assert.Null(await PaidAtAsync(failed));
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime Day() =>
        new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(0, 3000));

    private async Task<Guid> PlaceAsync(Guid seller, DateTime placedAt)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Currency = new Currency("VND", 0);
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        _fixture.Checkout.Prices[variant] = new CatalogPrice(
            product, "Camera", 1_000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller, "Mai's cameras");
        _fixture.Checkout.Cart = [new CartItem(product, 1, variant)];
        _fixture.Checkout.Address = Home;
        var order = (await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId;

        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == order)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.CreatedAt, placedAt));
        return order;
    }

    private async Task SettleAsync(Guid order, OrderStatus status, DateTime at, int expected = 1)
    {
        await using var scope = _fixture.NewScope();
        Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<IOrderRepository>().TrySettleAsync(order, status, null, at));
    }

    private async Task<DateTime?> PaidAtAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == order).Select(o => o.PaidAt).SingleAsync();
    }

    private async Task<T> AsAsync<T>(Guid caller, IRequest<T> request)
    {
        _fixture.CurrentUser.Id = caller;
        return await SendAsync(request);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
