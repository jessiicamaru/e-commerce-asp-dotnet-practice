using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.CancelOrder;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyBalance;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Cancelling a paid order (specs/039): who may, until when, exactly once - and that a cancelled order is
/// never money for a seller.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class CancellationTests
{
    private readonly OrderTestFixture _fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    public CancellationTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    // ------------------------------------------------------------------ US1: the customer

    [Fact]
    public async Task A_customer_cancels_a_paid_order_while_every_parcel_waits()
    {
        var (order, customer) = await PaidCheckoutAsync(Guid.CreateVersion7(), null);

        var detail = await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));

        Assert.Equal("Cancelled", detail.Status);
        Assert.Equal("Customer", detail.CancelledBy);
        var cancelled = Assert.Single(Published(order));
        Assert.Equal("Customer", cancelled.CancelledBy);
        Assert.Equal(OrderStatus.Cancelled, (await OrderSeed.ReadAsync(_fixture, order)).Status);
    }

    /// <summary>Principle III: a repeat affects nothing - and above all publishes nothing a second time.</summary>
    [Fact]
    public async Task Cancelling_twice_changes_nothing_and_undoes_once()
    {
        var (order, customer) = await PaidCheckoutAsync(Guid.CreateVersion7());
        await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));
        var before = await OrderSeed.ReadAsync(_fixture, order);

        var again = await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));

        Assert.Equal("Cancelled", again.Status);
        Assert.Equal(before.UpdatedAt, (await OrderSeed.ReadAsync(_fixture, order)).UpdatedAt);
        Assert.Single(Published(order));
    }

    [Fact]
    public async Task A_customer_cannot_cancel_once_a_parcel_is_being_prepared()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice, Guid.CreateVersion7());
        await MoveAsync(order, alice, ShipmentStatus.Pending, ShipmentStatus.Preparing);

        var refused = await Assert.ThrowsAsync<ConflictException>(
            () => As(customer, () => SendAsync(new CancelMyOrderCommand(order))));

        Assert.Equal(Cancellation.BeingPrepared, refused.Message);
        Assert.Equal(OrderStatus.Preparing, (await OrderSeed.ReadAsync(_fixture, order)).Status);
        Assert.Empty(Published(order));
    }

    [Fact]
    public async Task Someone_else_s_order_is_not_found_in_the_same_words_as_no_order()
    {
        var (order, _) = await PaidCheckoutAsync(Guid.CreateVersion7());
        var stranger = Guid.CreateVersion7();

        var notTheirs = await Assert.ThrowsAsync<NotFoundException>(
            () => As(stranger, () => SendAsync(new CancelMyOrderCommand(order))));
        var notThere = await Assert.ThrowsAsync<NotFoundException>(
            () => As(stranger, () => SendAsync(new CancelMyOrderCommand(Guid.CreateVersion7()))));

        Assert.Equal(notThere.Message, notTheirs.Message);
        Assert.Equal(OrderStatus.Paid, (await OrderSeed.ReadAsync(_fixture, order)).Status);
    }

    /// <summary>Still settling: nothing to undo yet. Failed: already nothing.</summary>
    [Theory]
    [InlineData(OrderStatus.Submitted)]
    [InlineData(OrderStatus.Failed)]
    public async Task An_order_that_is_not_paid_cannot_be_cancelled(OrderStatus status)
    {
        var (order, customer) = await CheckoutAsync(Guid.CreateVersion7());
        if (status != OrderStatus.Submitted)
        {
            await SettleAsync(order, status);
        }

        var refused = await Assert.ThrowsAsync<ConflictException>(
            () => As(customer, () => SendAsync(new CancelMyOrderCommand(order))));

        Assert.Equal(Cancellation.NotPaid, refused.Message);
        Assert.Empty(Published(order));
    }

    // ------------------------------------------------------------------ US3: staff, and the limit for everyone

    [Fact]
    public async Task Staff_can_cancel_while_a_parcel_is_being_prepared()
    {
        var alice = Guid.CreateVersion7();
        var (order, _) = await PaidCheckoutAsync(alice);
        await MoveAsync(order, alice, ShipmentStatus.Pending, ShipmentStatus.Preparing);

        var detail = await As(Guid.CreateVersion7(), () => SendAsync(new CancelOrderCommand(order)));

        Assert.Equal("Cancelled", detail.Status);
        Assert.Equal("Staff", detail.CancelledBy);
        Assert.Equal("Staff", Assert.Single(Published(order)).CancelledBy);
    }

    /// <summary>FR-001: one parcel out of the door and the order can no longer be cancelled - by anyone.</summary>
    [Fact]
    public async Task Nobody_can_cancel_once_a_parcel_has_shipped()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice, Guid.CreateVersion7());
        await MoveAsync(order, alice, ShipmentStatus.Pending, ShipmentStatus.Preparing);
        await MoveAsync(order, alice, ShipmentStatus.Preparing, ShipmentStatus.Shipped, "VNPOST-A");

        var staff = await Assert.ThrowsAsync<ConflictException>(
            () => As(Guid.CreateVersion7(), () => SendAsync(new CancelOrderCommand(order))));
        var theirs = await Assert.ThrowsAsync<ConflictException>(
            () => As(customer, () => SendAsync(new CancelMyOrderCommand(order))));

        Assert.Equal(Cancellation.Shipped, staff.Message);
        Assert.Equal(Cancellation.Shipped, theirs.Message);
        Assert.Empty(Published(order));
    }

    /// <summary>
    /// ⚠️ FR-003. Staff cancel while a seller ships, at the same moment, on many orders. Without the order's
    /// row lock both can succeed - a cancelled order with a parcel on its way and a refund for goods sent.
    /// Exactly one must win, every time.
    /// </summary>
    [Fact]
    public async Task Cancel_and_ship_at_once_leave_exactly_one_winner()
    {
        for (var i = 0; i < 12; i++)
        {
            var alice = Guid.CreateVersion7();
            var (order, _) = await PaidCheckoutAsync(alice);
            await MoveAsync(order, alice, ShipmentStatus.Pending, ShipmentStatus.Preparing);

            await Task.WhenAll(
                Swallow(async () =>
                {
                    await using var scope = _fixture.NewScope();
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CancelOrderCommand(order));
                }),
                Swallow(async () =>
                {
                    await using var scope = _fixture.NewScope();
                    await scope.ServiceProvider.GetRequiredService<IOrderRepository>().TryMoveShipmentAsync(
                        order, alice, ShipmentStatus.Preparing, ShipmentStatus.Shipped, "VNPOST-RACE", DateTime.UtcNow);
                }));

            var row = await OrderSeed.ReadAsync(_fixture, order);
            var shipped = (await PartsAsync(order)).Any(s => s == ShipmentStatus.Shipped);
            Assert.True(row.Status == OrderStatus.Cancelled ^ shipped,
                $"order {row.Status}, a part shipped: {shipped} - exactly one of the two must have happened");
        }
    }

    // ------------------------------------------------------------------ US2: never money

    [Fact]
    public async Task A_cancelled_order_leaves_every_balance()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        Assert.NotEmpty(await As(alice, () => SendAsync(new GetMyBalanceQuery())));

        await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));

        Assert.Empty(await As(alice, () => SendAsync(new GetMyBalanceQuery())));
    }

    /// <summary>
    /// ⚠️ A cancelled order's parts still exist, with their terms. Even one marked shipped straight in the
    /// table - which no endpoint allows - must never be paid out.
    /// </summary>
    [Fact]
    public async Task No_payout_ever_claims_a_cancelled_order()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
                .Where(s => s.OrderId == order)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, ShipmentStatus.Shipped));
        }

        await Assert.ThrowsAsync<ConflictException>(
            () => As(Guid.CreateVersion7(), () => SendAsync(new RecordPayoutCommand(alice, "VND"))));
    }

    /// <summary>research D5: the seller learns to stop - and nobody else learns anything.</summary>
    [Fact]
    public async Task Its_seller_sees_it_cancelled_and_cannot_move_it()
    {
        var alice = Guid.CreateVersion7();
        var carol = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));

        var sale = await As(alice, () => SendAsync(new GetMySaleQuery(order)));
        Assert.Equal("Cancelled", sale.Status);
        Assert.Null(sale.ShippingAddress);

        var hers = await Assert.ThrowsAsync<ConflictException>(
            () => As(alice, () => SendAsync(new PrepareMySaleCommand(order))));
        Assert.Equal(Sales.Cancelled, hers.Message);

        var notHers = await Assert.ThrowsAsync<NotFoundException>(
            () => As(carol, () => SendAsync(new PrepareMySaleCommand(order))));
        Assert.Equal(Sales.NotFound, notHers.Message);
    }

    // ------------------------------------------------------------------ helpers

    private List<OrderCancelledEvent> Published(Guid order) =>
        _fixture.Harness.Published.Select<OrderCancelledEvent>()
            .Where(x => x.Context.Message.OrderId == order)
            .Select(x => x.Context.Message)
            .ToList();

    private static async Task Swallow(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (ConflictException)
        {
        }
    }

    private async Task<T> As<T>(Guid user, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = user;
        return await body();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Checks out one line per seller (null = the shop); returns the order and its customer.</summary>
    private async Task<(Guid Order, Guid Customer)> CheckoutAsync(params Guid?[] sellers)
    {
        var customer = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = customer;
        var cart = new List<CartItem>();
        foreach (var seller in sellers)
        {
            var product = Guid.CreateVersion7();
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller, null);
            cart.Add(new CartItem(product, 1, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        var order = (await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId;
        return (order, customer);
    }

    private async Task<(Guid Order, Guid Customer)> PaidCheckoutAsync(params Guid?[] sellers)
    {
        var placed = await CheckoutAsync(sellers);
        await SettleAsync(placed.Order, OrderStatus.Paid);
        return placed;
    }

    private async Task SettleAsync(Guid order, OrderStatus status)
    {
        await using var scope = _fixture.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
            .TrySettleAsync(order, status, null, DateTime.UtcNow));
    }

    private async Task MoveAsync(Guid order, Guid? seller, ShipmentStatus from, ShipmentStatus to, string? tracking = null)
    {
        await using var scope = _fixture.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
            .TryMoveShipmentAsync(order, seller, from, to, tracking, DateTime.UtcNow);
        Assert.Equal(ShipmentMoveOutcome.Moved, result.Outcome);
    }

    private async Task<List<ShipmentStatus>> PartsAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.Where(s => s.OrderId == order).Select(s => s.Status).ToListAsync();
    }
}
