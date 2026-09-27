using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// The total as stored (feature 012): taxed by destination, and refused by the database when its parts
/// do not add up - not merely intended to.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class TotalsPersistenceTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;
    private readonly Guid _product = Guid.CreateVersion7();

    private async Task<OrderResponse> CheckoutToAsync(string country)
    {
        // These amounts - 9.99 a unit, 5.00 delivery - were always dollars; nothing said so until
        // specs/022 gave the shop currencies. Saying it here is what keeps the arithmetic below
        // right: dong has no decimal places, so the same cart priced in dong rounds differently.
        _fixture.Currency = new Ecommerce.Shared.Money.Currency("USD", 2);
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_product, 3)];
        _fixture.Checkout.Prices[_product] = new CatalogPrice(_product, "Widget", 9.99m, Sellable: true);
        _fixture.Checkout.Address = new AddressCopy("A", "1 St", null, "City", null, "12345", country, null);

        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SubmitOrderCommand(null, "standard"));
    }

    [Fact]
    public async Task The_same_cart_to_two_destinations_has_equal_subtotals_and_different_tax()
    {
        var vn = await CheckoutToAsync("VN");   // 10%
        var gb = await CheckoutToAsync("GB");   // 20%

        Assert.Equal(vn.Subtotal, gb.Subtotal);
        Assert.Equal(0.10m, vn.TaxRate);
        Assert.Equal(0.20m, gb.TaxRate);
        // 29.97 x 10% = 3.00 (2.997), delivery 5.00 x 10% = 0.50
        Assert.Equal(3.50m, vn.TaxTotal);
        // 29.97 x 20% = 5.99 (5.994), delivery 5.00 x 20% = 1.00
        Assert.Equal(6.99m, gb.TaxTotal);
    }

    /// <summary>
    /// #186 (specs/094): the rates are read once, at startup, so "changed in configuration" means a restart. An order
    /// placed before it keeps the rate and the tax it was charged (specs/012) - read back through the restarted service,
    /// whose new checkouts do use the new rate (which is what shows the restart took effect).
    /// </summary>
    [Fact]
    public async Task A_rate_changed_after_the_order_does_not_change_it()
    {
        var placed = await CheckoutToAsync("VN");   // 10%

        await using var restarted = _fixture.RestartedWith(s => s.AddSingleton<ITaxRates>(new FixedRate(0.25m)));
        await restarted.GetRequiredService<ITestHarness>().Start();
        await using var scope = restarted.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var read = await sender.Send(new GetMyOrderByIdQuery(placed.OrderId));
        Assert.Equal((0.10m, 3.50m, placed.TotalAmount), (read.TaxRate, read.TaxTotal, read.TotalAmount));
        Assert.Equal(3.00m, Assert.Single(read.Items).TaxAmount);

        var afterRestart = await sender.Send(new SubmitOrderCommand(null, "standard"));
        Assert.Equal(0.25m, afterRestart.TaxRate);
    }

    private sealed class FixedRate(decimal rate) : ITaxRates
    {
        public decimal RateFor(string country) => rate;
    }

    [Fact]
    public async Task An_unlisted_destination_gets_the_default_rate_and_says_so()
    {
        var fr = await CheckoutToAsync("FR");

        Assert.Equal(0.10m, fr.TaxRate);
    }

    [Fact]
    public async Task Stored_parts_sum_to_the_stored_total_and_each_line_keeps_its_tax()
    {
        var response = await CheckoutToAsync("GB");
        var row = await OrderSeed.ReadAsync(_fixture, response.OrderId);

        Assert.Equal(row.TotalAmount, row.Subtotal + row.ShippingPrice + row.TaxTotal - row.DiscountTotal);
        Assert.Equal(5.99m, Assert.Single(row.Items).TaxAmount);
        Assert.Equal(0m, row.DiscountTotal);
    }

    [Fact]
    public async Task The_database_refuses_a_total_whose_parts_do_not_add_up()
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var id = Guid.CreateVersion7();

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = id,
            UserId = Guid.CreateVersion7(),
            Status = OrderStatus.Submitted,
            Subtotal = 10m,
            ShippingPrice = 5m,
            TaxTotal = 1.50m,
            DiscountTotal = 0m,
            TaxRate = 0.10m,
            TotalAmount = 99m,   // should be 16.50
            Items =
            [
                new OrderItem
                {
                    Id = Guid.CreateVersion7(), OrderId = id, ProductId = Guid.CreateVersion7(),
                    ProductName = "X", Quantity = 1, UnitPrice = 10m
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_orders_parts_sum_to_total", ex.InnerException?.Message ?? ex.Message);
    }
}
