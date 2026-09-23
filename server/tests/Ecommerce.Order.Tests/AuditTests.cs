using Ecommerce.Contracts.Activity;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.CancelOrder;
using Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What Order tells the audit log (specs/041): an order's whole life - placed, a parcel prepared, shipped,
/// received, a payout for it - one entry per step, and none for a step that was refused.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class AuditTests
{
    private readonly OrderTestFixture _fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    public AuditTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    [Fact]
    public async Task An_order_s_life_is_recorded_one_step_at_a_time()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);

        await As(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-AUD")));
        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-AUD")));   // a repeat: no entry
        var parcel = await PartIdAsync(order, alice);
        await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));
        await As(Guid.CreateVersion7(), () => SendAsync(new RecordPayoutCommand(alice, "VND")));

        var steps = Entries(order.ToString()).Select(e => e.Action).ToList();
        Assert.Equal(["OrderPlaced", "ParcelPrepared", "ParcelShipped", "ParcelReceived"], steps);

        var shipped = Entries(order.ToString()).Single(e => e.Action == "ParcelShipped");
        Assert.Equal(alice, shipped.ActorId);
        Assert.Contains("VNPOST-AUD", shipped.After);

        var payout = Assert.Single(Entries(alice.ToString()));
        Assert.Equal(("Payment", "PayoutRecorded"), (payout.Category, payout.Action));
    }

    [Fact]
    public async Task A_cancellation_is_recorded_with_who_cancelled()
    {
        var (order, customer) = await PaidCheckoutAsync(Guid.CreateVersion7());

        await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));
        await As(customer, () => SendAsync(new CancelMyOrderCommand(order)));   // repeated: no second entry

        var cancelled = Assert.Single(Entries(order.ToString()), e => e.Action == "OrderCancelled");
        Assert.Equal(customer, cancelled.ActorId);
        Assert.Contains("Cancelled", cancelled.After);
    }

    /// <summary>A step the rules refuse writes nothing - and so claims nothing happened.</summary>
    [Fact]
    public async Task A_refused_step_is_not_recorded()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        var parcel = await PartIdAsync(order, alice);

        await Assert.ThrowsAsync<ConflictException>(
            () => As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel))));   // not shipped yet

        Assert.Equal(["OrderPlaced"], Entries(order.ToString()).Select(e => e.Action));
    }

    [Fact]
    public async Task The_delivery_sweep_is_a_system_entry()
    {
        var alice = Guid.CreateVersion7();
        var (order, _) = await PaidCheckoutAsync(alice);
        await As(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-SWEEP")));
        var cutoff = DateTime.UtcNow.AddDays(-7);
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
                .Where(s => s.OrderId == order)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.ShippedAt, cutoff.AddHours(-1)));
        }

        var count = await SendAsync(new AutoConfirmDeliveriesCommand(cutoff));

        var sweep = _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Last(e => e.Action == "DeliveriesAutoConfirmed");
        Assert.Equal("System", sweep.Category);
        Assert.Contains($"\"count\":{count}", sweep.After);
    }

    // ------------------------------------------------------------------ helpers

    private List<AuditEntryRecorded> Entries(string subject) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>()
            .Select(x => x.Context.Message)
            .Where(e => e.SubjectId == subject)
            .OrderBy(e => e.OccurredAt)
            .ToList();

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

    private async Task<(Guid Order, Guid Customer)> PaidCheckoutAsync(params Guid?[] sellers)
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

        await using var scope = _fixture.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
            .TrySettleAsync(order, OrderStatus.Paid, null, DateTime.UtcNow));
        return (order, customer);
    }

    private async Task<Guid> PartIdAsync(Guid order, Guid seller)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.Where(s => s.OrderId == order && s.SellerId == seller).Select(s => s.Id).SingleAsync();
    }
}
