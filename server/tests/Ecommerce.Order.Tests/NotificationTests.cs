using Ecommerce.Contracts.Activity;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.CancelOrder;
using Ecommerce.Order.Application.Orders.Commands.CompleteOrder;
using Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;
using Ecommerce.Order.Application.Orders.Commands.FailOrder;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Notifications;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Who is told what as an order moves (specs/042) - the right people, once each, and nobody else.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class NotificationTests
{
    private readonly OrderTestFixture _fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    public NotificationTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    [Fact]
    public async Task A_paid_order_tells_its_buyer_and_each_seller_once()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var (order, buyer) = await PlacedAsync(alice, bob, null);

        await SendAsync(new CompleteOrderCommand(order, DateTime.UtcNow));
        await SendAsync(new CompleteOrderCommand(order, DateTime.UtcNow));   // redelivered: nobody told twice

        var sent = Sent(order);
        var paid = Assert.Single(sent, n => n.Kind == "OrderPaid");
        Assert.Equal(buyer, paid.RecipientId);
        Assert.Equal("VND", paid.Data["currency"]);
        Assert.Equal($"/orders/{order}", paid.Link);
        Assert.Equal(new HashSet<Guid> { alice, bob }, sent.Where(n => n.Kind == "NewSale").Select(n => n.RecipientId).ToHashSet());
        Assert.Equal(2, sent.Count(n => n.Kind == "NewSale"));
    }

    /// <summary>
    /// The saga's outcome arrives at a consumer, and MassTransit's consumer outbox has already opened a
    /// transaction on the context by then. Settling must join it, not open a second one - which threw,
    /// left every order Submitted, and passed every test that sent the command outside a consumer.
    /// </summary>
    [Fact]
    public async Task Settling_inside_a_consumer_transaction_joins_it()
    {
        var (order, _) = await PlacedAsync(Guid.CreateVersion7());

        await using (var scope = _fixture.NewScope())
        {
            // The way MassTransit's EF outbox opens it: inside the context's execution strategy, which the fixture
            // now configures as production does (retries on), and which refuses a transaction opened outside one.
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var consumer = await db.Database.BeginTransactionAsync();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CompleteOrderCommand(order, DateTime.UtcNow));
                await consumer.CommitAsync();
            });
        }

        await using var read = _fixture.NewScope();
        var status = await read.ServiceProvider.GetRequiredService<OrderDbContext>()
            .Orders.Where(o => o.Id == order).Select(o => o.Status).SingleAsync();
        Assert.Equal(Ecommerce.Order.Domain.Enums.OrderStatus.Paid, status);
        Assert.Single(Sent(order), n => n.Kind == "OrderPaid");
    }

    [Fact]
    public async Task A_failed_order_tells_its_buyer_and_no_seller()
    {
        var (order, buyer) = await PlacedAsync(Guid.CreateVersion7());

        await SendAsync(new FailOrderCommand(order, "Payment declined", DateTime.UtcNow));

        var only = Assert.Single(Sent(order));
        Assert.Equal(("OrderFailed", buyer), (only.Kind, only.RecipientId));
    }

    [Fact]
    public async Task Shipping_tells_the_buyer_with_the_tracking_and_receiving_tells_the_seller()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer) = await PaidAsync(alice);

        await As(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-NOTE")));
        var parcel = await PartIdAsync(order, alice);
        await As(buyer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));

        var shipped = Assert.Single(Sent(order), n => n.Kind == "ParcelShipped");
        Assert.Equal(buyer, shipped.RecipientId);
        Assert.Equal("VNPOST-NOTE", shipped.Data["tracking"]);
        Assert.Equal("Shop of " + alice.ToString("N")[..6], shipped.Data["shop"]);

        var received = Assert.Single(Sent(order), n => n.Kind == "ParcelReceived");
        Assert.Equal(alice, received.RecipientId);
        Assert.Equal($"/shop/sales/{order}", received.Link);
    }

    /// <summary>
    /// #102 (specs/060): a paid order is confirmed by email to its buyer, once, in the language it was placed
    /// in - requested in the settlement's own transaction; a failed order asks for none.
    /// </summary>
    [Fact]
    public async Task A_paid_order_asks_for_one_confirmation_email_in_its_language_and_a_failed_one_for_none()
    {
        var (order, buyer) = await PaidAsync(Guid.CreateVersion7());
        await SendAsync(new CompleteOrderCommand(order, DateTime.UtcNow));   // a redelivered settlement

        var asked = _fixture.Harness.Published.Select<Ecommerce.Contracts.Identity.EmailRequested>()
            .Select(x => x.Context.Message).Where(m => m.Data.GetValueOrDefault("orderId") == order.ToString()).ToList();
        var email = Assert.Single(asked);
        Assert.Equal((buyer, "OrderPaid"), (email.RecipientId, email.Template));
        await using (var scope = _fixture.NewScope())
        {
            var language = await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders
                .Where(o => o.Id == order).Select(o => o.Language).SingleAsync();
            Assert.Equal(language, email.Language);
        }

        var (failed, _) = await PlacedAsync(Guid.CreateVersion7());
        await SendAsync(new FailOrderCommand(failed, "Payment declined", DateTime.UtcNow));
        Assert.DoesNotContain(_fixture.Harness.Published.Select<Ecommerce.Contracts.Identity.EmailRequested>(),
            x => x.Context.Message.Data.GetValueOrDefault("orderId") == failed.ToString());
    }

    /// <summary>
    /// #128 (specs/059): the seller was told when the CUSTOMER confirmed and not at all when the 7-day sweep
    /// did - though that is the moment their money becomes due.
    /// </summary>
    [Fact]
    public async Task The_sweep_taking_a_parcel_as_delivered_tells_its_seller()
    {
        var alice = Guid.CreateVersion7();
        var (order, _) = await PaidAsync(alice);
        await As(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-AUTO")));
        var cutoff = DateTime.UtcNow.AddDays(-7);
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
                .Where(s => s.OrderId == order && s.SellerId == alice)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.ShippedAt, cutoff.AddHours(-1)));
        }

        await SendAsync(new AutoConfirmDeliveriesCommand(cutoff));

        var told = Assert.Single(Sent(order), n => n.Kind == "ParcelAutoDelivered");
        Assert.Equal((alice, $"/shop/sales/{order}"), (told.RecipientId, told.Link));
        Assert.DoesNotContain(Sent(order), n => n.Kind == "ParcelReceived");   // nobody clicked
    }

    /// <summary>
    /// #167 (specs/083): a parcel on its way and a cancelled order reached the buyer only in the bell. Each is now
    /// one email, in the order's language - and preparing, which is not news, is none.
    /// </summary>
    [Fact]
    public async Task Shipping_and_cancelling_each_ask_for_one_email_in_the_orders_language()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer) = await PaidAsync(alice);
        await As(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        Assert.Empty(Emailed(order, "ParcelShipped"));

        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-MAIL")));
        await Record.ExceptionAsync(() => As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-MAIL"))));   // again: refused

        var shipped = Assert.Single(Emailed(order, "ParcelShipped"));
        Assert.Equal((buyer, await LanguageAsync(order)), (shipped.RecipientId, shipped.Language));
        Assert.Equal(("VNPOST-MAIL", "Shop of " + alice.ToString("N")[..6]), (shipped.Data["tracking"], shipped.Data["shop"]));

        var (other, otherBuyer) = await PaidAsync(alice, null);
        await As(otherBuyer, () => SendAsync(new CancelMyOrderCommand(other)));
        await Record.ExceptionAsync(() => As(otherBuyer, () => SendAsync(new CancelMyOrderCommand(other))));   // again: nothing new

        var cancelled = Assert.Single(Emailed(other, "OrderCancelled"));
        Assert.Equal((otherBuyer, await LanguageAsync(other)), (cancelled.RecipientId, cancelled.Language));
        await using var scope = _fixture.NewScope();
        var total = await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == other).Select(o => o.TotalAmount).SingleAsync();
        Assert.Equal((total.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), "VND"), (cancelled.Data["total"], cancelled.Data["currency"]));
    }

    [Fact]
    public async Task A_cancellation_tells_the_buyer_and_every_seller()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer) = await PaidAsync(alice, null);

        await As(buyer, () => SendAsync(new CancelMyOrderCommand(order)));

        var sent = Sent(order);
        var cancelled = Assert.Single(sent, n => n.Kind == "OrderCancelled");
        Assert.Equal(("Customer", buyer), (cancelled.Data["by"], cancelled.RecipientId));
        Assert.Equal(alice, Assert.Single(sent, n => n.Kind == "SaleCancelled").RecipientId);
    }

    [Fact]
    public async Task A_payout_tells_its_seller_how_much()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer) = await PaidAsync(alice);
        await As(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await As(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-PAY")));
        var parcel = await PartIdAsync(order, alice);
        await As(buyer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));
        await ReturnWindow.PassAsync(_fixture, order);   // due only after the return window (specs/066)

        var payout = await As(Guid.CreateVersion7(), () => SendAsync(new RecordPayoutCommand(alice, "VND")));

        var told = _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Single(n => n.Kind == "PayoutRecorded" && n.RecipientId == alice);
        Assert.Equal(payout.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), told.Data["amount"]);
        Assert.Equal("/shop/payouts", told.Link);
        Assert.Empty(NotificationContract.Problems(told.Kind, told.Data));
    }

    /// <summary>
    /// The kinds a service can send and the kinds the declaration the storefront reads names are one set
    /// (specs/048) - a kind added to one and not the other is how #119 happened.
    /// </summary>
    [Fact]
    public void Every_kind_in_code_is_declared_for_the_storefront_and_nothing_else_is()
    {
        Assert.Equal(NotificationContract.KindsInCode.Order(), NotificationContract.Kinds.Keys.Order());
    }

    // ------------------------------------------------------------------ helpers

    private List<Ecommerce.Contracts.Identity.EmailRequested> Emailed(Guid order, string template) =>
        _fixture.Harness.Published.Select<Ecommerce.Contracts.Identity.EmailRequested>().Select(x => x.Context.Message)
            .Where(m => m.Template == template && m.Data.GetValueOrDefault("orderId") == order.ToString())
            .ToList();

    private async Task<string?> LanguageAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders
            .Where(o => o.Id == order).Select(o => o.Language).SingleAsync();
    }

    /// <summary>What was sent about an order - each checked against what the storefront reads (specs/048).</summary>
    private List<UserNotificationRequested> Sent(Guid order)
    {
        var sent = _fixture.Harness.Published.Select<UserNotificationRequested>()
            .Select(x => x.Context.Message)
            .Where(n => n.Data.TryGetValue("orderId", out var id) && id == order.ToString())
            .ToList();
        Assert.Empty(sent.SelectMany(n => NotificationContract.Problems(n.Kind, n.Data)));
        return sent;
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

    private async Task<(Guid Order, Guid Buyer)> PlacedAsync(params Guid?[] sellers)
    {
        var buyer = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = buyer;
        var cart = new List<CartItem>();
        foreach (var seller in sellers)
        {
            var product = Guid.CreateVersion7();
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller,
                seller is { } s ? "Shop of " + s.ToString("N")[..6] : null);
            cart.Add(new CartItem(product, 1, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        return ((await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId, buyer);
    }

    private async Task<(Guid Order, Guid Buyer)> PaidAsync(params Guid?[] sellers)
    {
        var placed = await PlacedAsync(sellers);
        await SendAsync(new CompleteOrderCommand(placed.Order, DateTime.UtcNow));
        return placed;
    }

    private async Task<Guid> PartIdAsync(Guid order, Guid seller)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.Where(s => s.OrderId == order && s.SellerId == seller).Select(s => s.Id).SingleAsync();
    }
}
