using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyBalance;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Delivery;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A parcel is delivered when its customer says so, or a week after it shipped when nobody does - and only
/// a delivered parcel is money for its seller (specs/040).
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class DeliveryTests
{
    private readonly OrderTestFixture _fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    public DeliveryTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    // ------------------------------------------------------------------ US1: the customer confirms

    [Fact]
    public async Task Shipping_records_when_it_shipped()
    {
        var alice = Guid.CreateVersion7();
        var (order, _) = await PaidCheckoutAsync(alice);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await ShipAsync(order, alice);

        var part = await PartAsync(order, alice);
        Assert.NotNull(part.ShippedAt);
        Assert.True(part.ShippedAt >= before);
        Assert.Null(part.DeliveredAt);
    }

    [Fact]
    public async Task The_customer_confirms_a_shipped_parcel()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice, Guid.CreateVersion7());
        await ShipAsync(order, alice);
        var parcel = (await PartAsync(order, alice)).Id;

        var detail = await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));

        var shown = Assert.Single(detail.Shipments!, s => s.Id == parcel);
        Assert.NotNull(shown.DeliveredAt);
        Assert.Equal("Customer", shown.DeliveryConfirmedBy);
        // The other seller's parcel is untouched: one parcel, one confirmation.
        Assert.All(detail.Shipments!.Where(s => s.Id != parcel), s => Assert.Null(s.DeliveredAt));
    }

    [Fact]
    public async Task A_parcel_not_shipped_yet_cannot_be_received()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        var parcel = (await PartAsync(order, alice)).Id;

        var refused = await Assert.ThrowsAsync<ConflictException>(
            () => As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel))));

        Assert.Equal(ParcelDelivery.NotShipped, refused.Message);
        Assert.Null((await PartAsync(order, alice)).DeliveredAt);
    }

    [Fact]
    public async Task Confirming_twice_changes_nothing()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        await ShipAsync(order, alice);
        var parcel = (await PartAsync(order, alice)).Id;
        await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));
        var first = (await PartAsync(order, alice)).DeliveredAt;

        await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));

        Assert.Equal(first, (await PartAsync(order, alice)).DeliveredAt);
    }

    /// <summary>
    /// Not theirs, a parcel of another order, no such order: one wording, and nothing recorded - saying a
    /// stranger's parcel arrived would release a seller's money on a stranger's word.
    /// </summary>
    [Fact]
    public async Task Someone_else_s_parcel_is_not_found()
    {
        var alice = Guid.CreateVersion7();
        var (order, _) = await PaidCheckoutAsync(alice);
        var (other, otherCustomer) = await PaidCheckoutAsync(Guid.CreateVersion7());
        await ShipAsync(order, alice);
        var parcel = (await PartAsync(order, alice)).Id;

        var stranger = await Assert.ThrowsAsync<NotFoundException>(
            () => As(Guid.CreateVersion7(), () => SendAsync(new ConfirmDeliveryCommand(order, parcel))));
        var wrongOrder = await Assert.ThrowsAsync<NotFoundException>(
            () => As(otherCustomer, () => SendAsync(new ConfirmDeliveryCommand(other, parcel))));

        Assert.Equal(ParcelDelivery.NotFound, stranger.Message);
        Assert.Equal(ParcelDelivery.NotFound, wrongOrder.Message);
        Assert.Null((await PartAsync(order, alice)).DeliveredAt);
    }

    // ------------------------------------------------------------------ US2: money follows delivery

    /// <summary>⚠️ SC-001: shipped is not enough any more - a parcel marked shipped and never sent is not due.</summary>
    [Fact]
    public async Task A_shipped_parcel_is_on_the_way_until_it_is_delivered()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        await ShipAsync(order, alice);
        const decimal owed = 1000m - 100m + 30000m;

        Assert.Equal(new BalanceResponse("VND", owed, 0m, 0m), Assert.Single(await BalanceAsync(alice)));
        await Assert.ThrowsAsync<ConflictException>(
            () => As(Guid.CreateVersion7(), () => SendAsync(new RecordPayoutCommand(alice, "VND"))));

        var aliceParcel = (await PartAsync(order, alice)).Id;
        await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, aliceParcel)));

        Assert.Equal(new BalanceResponse("VND", 0m, owed, 0m), Assert.Single(await BalanceAsync(alice)));
        var payout = await As(Guid.CreateVersion7(), () => SendAsync(new RecordPayoutCommand(alice, "VND")));
        Assert.Equal(owed, payout.Amount);
    }

    [Fact]
    public async Task The_seller_sees_their_parcel_was_received()
    {
        var alice = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(alice);
        await ShipAsync(order, alice);
        var aliceParcel = (await PartAsync(order, alice)).Id;
        await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, aliceParcel)));

        var sale = await As(alice, () => SendAsync(new GetMySaleQuery(order)));

        Assert.NotNull(sale.DeliveredAt);
    }

    // ------------------------------------------------------------------ US3: nobody waits forever

    [Fact]
    public async Task Parcels_shipped_before_the_cutoff_are_taken_as_delivered()
    {
        var old = Guid.CreateVersion7();
        var recent = Guid.CreateVersion7();
        var waiting = Guid.CreateVersion7();
        var confirmed = Guid.CreateVersion7();
        var (order, customer) = await PaidCheckoutAsync(old, recent, waiting, confirmed);
        foreach (var seller in new[] { old, recent, confirmed })
        {
            await ShipAsync(order, seller);
        }

        var confirmedParcel = (await PartAsync(order, confirmed)).Id;
        await As(customer, () => SendAsync(new ConfirmDeliveryCommand(order, confirmedParcel)));
        var confirmedAt = (await PartAsync(order, confirmed)).DeliveredAt;
        var cutoff = DateTime.UtcNow.AddDays(-7);
        await BackdateShippedAsync(order, old, cutoff.AddHours(-1));
        await BackdateShippedAsync(order, confirmed, cutoff.AddHours(-1));

        await SendAsync(new AutoConfirmDeliveriesCommand(cutoff));

        var oldPart = await PartAsync(order, old);
        Assert.NotNull(oldPart.DeliveredAt);
        Assert.Equal("Auto", oldPart.DeliveryConfirmedBy);
        Assert.Null((await PartAsync(order, recent)).DeliveredAt);    // shipped inside the week
        Assert.Null((await PartAsync(order, waiting)).DeliveredAt);   // never shipped
        var customerSaid = await PartAsync(order, confirmed);
        Assert.Equal("Customer", customerSaid.DeliveryConfirmedBy);   // the customer's word stands
        Assert.Equal(confirmedAt, customerSaid.DeliveredAt);
    }

    /// <summary>Two instances sweeping at once, or one sweeping twice: the second changes nothing.</summary>
    [Fact]
    public async Task Sweeping_twice_delivers_once()
    {
        var alice = Guid.CreateVersion7();
        var (order, _) = await PaidCheckoutAsync(alice);
        await ShipAsync(order, alice);
        var cutoff = DateTime.UtcNow.AddDays(-7);
        await BackdateShippedAsync(order, alice, cutoff.AddHours(-1));

        var sweeps = await Task.WhenAll(
            SendAsync(new AutoConfirmDeliveriesCommand(cutoff)),
            SendAsync(new AutoConfirmDeliveriesCommand(cutoff)));
        var again = await SendAsync(new AutoConfirmDeliveriesCommand(cutoff));

        Assert.True(sweeps.Sum() >= 1);
        Assert.Equal(0, again);
        Assert.Equal("Auto", (await PartAsync(order, alice)).DeliveryConfirmedBy);
    }

    [Theory]
    [InlineData("0", "60")]
    [InlineData("7", "0")]
    [InlineData("-1", "60")]
    public void Order_does_not_start_without_a_sensible_delivery_period(string days, string interval)
    {
        var services = new ServiceCollection();
        DeliveryOptions.Register(services, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Delivery:AutoConfirmDays"] = days,
                ["Delivery:SweepIntervalMinutes"] = interval
            })
            .Build());

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DeliveryOptions>>().Value);
    }

    // ------------------------------------------------------------------ helpers

    private Task<List<BalanceResponse>> BalanceAsync(Guid seller) =>
        As(seller, () => SendAsync(new GetMyBalanceQuery()));

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

    private async Task ShipAsync(Guid order, Guid? seller)
    {
        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        Assert.Equal(ShipmentMoveOutcome.Moved, (await repository.TryMoveShipmentAsync(
            order, seller, ShipmentStatus.Pending, ShipmentStatus.Preparing, null, DateTime.UtcNow)).Outcome);
        Assert.Equal(ShipmentMoveOutcome.Moved, (await repository.TryMoveShipmentAsync(
            order, seller, ShipmentStatus.Preparing, ShipmentStatus.Shipped, "VNPOST", DateTime.UtcNow)).Outcome);
    }

    private async Task BackdateShippedAsync(Guid order, Guid seller, DateTime shippedAt)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
            .Where(s => s.OrderId == order && s.SellerId == seller)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.ShippedAt, shippedAt));
    }

    private async Task<OrderShipment> PartAsync(Guid order, Guid? seller)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.AsNoTracking().SingleAsync(s => s.OrderId == order && s.SellerId == seller);
    }
}
