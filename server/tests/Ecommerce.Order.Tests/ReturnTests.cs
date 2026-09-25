using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyBalance;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Application.Returns;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Notifications;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Sending a delivered parcel back (specs/066, #107): asked within the window, decided by its seller (staff for
/// the shop's parcel, and for a dispute), sent back, received - and then refunded and restocked, once.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class ReturnTests
{
    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private readonly OrderTestFixture _fixture;

    public ReturnTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    // ------------------------------------------------------------------ US1: asking

    [Fact]
    public async Task The_buyer_asks_to_return_a_delivered_parcel_and_its_seller_is_told()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await DeliveredAsync(alice);

        var asked = await As(buyer, () => SendAsync(new RequestReturnCommand(order, parcel, "  The lens is scratched ")));

        Assert.Equal(("Requested", "The lens is scratched", false), (asked.Status, asked.Reason, asked.IsShop));
        Assert.Contains(Told(order), n => n.RecipientId == alice && n.Kind == NotificationKind.ReturnRequested);
    }

    [Fact]
    public async Task Somebody_else_s_parcel_is_not_found()
    {
        var (order, _, parcel) = await DeliveredAsync(Guid.CreateVersion7());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            As(Guid.CreateVersion7(), () => SendAsync(new RequestReturnCommand(order, parcel, "Mine now"))));
    }

    [Fact]
    public async Task A_parcel_not_delivered_or_past_its_window_cannot_be_returned()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer) = await PaidCheckoutAsync(alice);
        await ShipAsync(order, alice);
        var parcel = (await PartAsync(order, alice)).Id;

        await Assert.ThrowsAsync<ConflictException>(() => As(buyer, () => SendAsync(new RequestReturnCommand(order, parcel, "Not here yet"))));

        await As(buyer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));
        await ReturnWindow.PassAsync(_fixture, order);

        var late = await Assert.ThrowsAsync<ConflictException>(() => As(buyer, () => SendAsync(new RequestReturnCommand(order, parcel, "Too late"))));
        Assert.Contains("within 7 days", late.Message);
    }

    [Fact]
    public async Task A_parcel_is_returned_once_even_asked_twice_at_once()
    {
        var (order, buyer, parcel) = await DeliveredAsync(Guid.CreateVersion7());
        _fixture.CurrentUser.Id = buyer;

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try { await SendAsync(new RequestReturnCommand(order, parcel, "Broken")); return true; }
            catch (ConflictException) { return false; }
        }));

        Assert.Equal(1, outcomes.Count(ok => ok));
        Assert.Equal(1, await CountAsync(parcel));
    }

    [Fact]
    public async Task A_reason_is_required()
    {
        var (order, buyer, parcel) = await DeliveredAsync(Guid.CreateVersion7());

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => As(buyer, () => SendAsync(new RequestReturnCommand(order, parcel, " "))));
    }

    // ------------------------------------------------------------------ US2: deciding

    [Fact]
    public async Task The_seller_accepts_and_the_buyer_is_told()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);

        var accepted = await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, true, null)));

        Assert.Equal("Accepted", accepted.Status);
        Assert.Contains(Told(order), n => n.RecipientId == buyer && n.Kind == NotificationKind.ReturnAccepted);
    }

    [Fact]
    public async Task A_refusal_needs_a_reason_and_the_buyer_reads_it()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, _) = await RequestedAsync(alice);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, ""))));
        var refused = await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "Used, not faulty")));

        Assert.Equal(("Refused", "Used, not faulty"), (refused.Status, refused.DecisionReason));
        var notice = Assert.Single(Told(order), n => n.Kind == NotificationKind.ReturnRefused);
        Assert.Equal((buyer, "Used, not faulty"), (notice.RecipientId, notice.Data["reason"]));
    }

    [Fact]
    public async Task Only_the_parcel_s_seller_decides_it_and_only_once()
    {
        var alice = Guid.CreateVersion7();
        var (order, _, _) = await RequestedAsync(alice);

        await Assert.ThrowsAsync<NotFoundException>(() => As(Guid.CreateVersion7(), () => SendAsync(new DecideSaleReturnCommand(order, true, null))));
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, true, null)));
        await Assert.ThrowsAsync<ConflictException>(() => As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "Changed my mind"))));
    }

    [Fact]
    public async Task Staff_answer_the_shop_s_own_parcel_but_not_a_seller_s_until_it_is_escalated()
    {
        var (shopOrder, _, shopParcel) = await RequestedAsync(null);
        Assert.Equal("Accepted", (await AsStaff(() => SendAsync(new DecideReturnCommand(shopOrder, shopParcel, true, null)))).Status);

        var (order, _, parcel) = await RequestedAsync(Guid.CreateVersion7());
        await Assert.ThrowsAsync<ConflictException>(() => AsStaff(() => SendAsync(new DecideReturnCommand(order, parcel, true, null))));
    }

    [Fact]
    public async Task A_refusal_can_be_escalated_and_staff_have_the_final_word()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "Used, not faulty")));

        Assert.Equal("Escalated", (await As(buyer, () => SendAsync(new EscalateReturnCommand(order, parcel)))).Status);
        // The seller no longer decides an escalated return.
        await Assert.ThrowsAsync<ConflictException>(() => As(alice, () => SendAsync(new DecideSaleReturnCommand(order, true, null))));

        var rejected = await AsStaff(() => SendAsync(new DecideReturnCommand(order, parcel, false, "The photos show wear")));
        Assert.Equal(("Rejected", "The photos show wear"), (rejected.Status, rejected.DecisionReason));
        await Assert.ThrowsAsync<ConflictException>(() => As(buyer, () => SendAsync(new EscalateReturnCommand(order, parcel))));
    }

    [Fact]
    public async Task Staff_can_overrule_a_seller_s_refusal()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "No")));
        await As(buyer, () => SendAsync(new EscalateReturnCommand(order, parcel)));

        Assert.Equal("Accepted", (await AsStaff(() => SendAsync(new DecideReturnCommand(order, parcel, true, null)))).Status);
    }

    [Fact]
    public async Task A_refusal_is_escalated_within_the_window_or_not_at_all()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "No")));
        await AgeDecisionAsync(parcel);

        await Assert.ThrowsAsync<ConflictException>(() => As(buyer, () => SendAsync(new EscalateReturnCommand(order, parcel))));
    }

    // ------------------------------------------------------------------ US3: back, received, refunded

    [Fact]
    public async Task Sent_back_with_a_reference_and_the_seller_is_told()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await AcceptedAsync(alice);

        var sent = await As(buyer, () => SendAsync(new SendReturnBackCommand(order, parcel, "VNPOST-BACK-1")));

        Assert.Equal(("SentBack", "VNPOST-BACK-1"), (sent.Status, sent.TrackingReference));
        var notice = Assert.Single(Told(order), n => n.Kind == NotificationKind.ReturnSentBack);
        Assert.Equal((alice, "VNPOST-BACK-1"), (notice.RecipientId, notice.Data["tracking"]));
    }

    [Fact]
    public async Task An_accepted_return_not_sent_back_within_the_window_lapses()
    {
        var (order, buyer, parcel) = await AcceptedAsync(Guid.CreateVersion7());
        await AgeDecisionAsync(parcel);

        await Assert.ThrowsAsync<ConflictException>(() => As(buyer, () => SendAsync(new SendReturnBackCommand(order, parcel, "LATE"))));
    }

    [Fact]
    public async Task Received_announces_the_lines_and_the_refund_of_goods_and_tax_once()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await SentBackAsync(alice, quantity: 2);

        var received = await As(alice, () => SendAsync(new ReceiveSaleReturnCommand(order)));

        // 2 x 1,000 goods + 10% tax on them = 2,200. The delivery share is not refunded.
        Assert.Equal(("Received", 2200m), (received.Status, received.RefundAmount));
        var returned = Assert.Single(_fixture.Harness.Published.Select<ParcelReturnedEvent>().Select(x => x.Context.Message), e => e.OrderId == order);
        Assert.Equal((received.Id, parcel, 2200m, "VND"), (returned.ReturnId, returned.ShipmentId, returned.Amount, returned.Currency));
        Assert.Equal(2, Assert.Single(returned.Items).Quantity);
        Assert.Contains(Told(order), n => n.RecipientId == buyer && n.Kind == NotificationKind.ReturnRefunded);

        await Assert.ThrowsAsync<ConflictException>(() => As(alice, () => SendAsync(new ReceiveSaleReturnCommand(order))));
        Assert.Single(_fixture.Harness.Published.Select<ParcelReturnedEvent>(), e => e.Context.Message.OrderId == order);
    }

    [Fact]
    public async Task Staff_receive_the_shop_s_own_parcel_but_not_a_seller_s()
    {
        var (shopOrder, _, shopParcel) = await SentBackAsync(null);
        Assert.Equal("Received", (await AsStaff(() => SendAsync(new ReceiveReturnCommand(shopOrder, shopParcel)))).Status);

        var (order, _, parcel) = await SentBackAsync(Guid.CreateVersion7());
        await Assert.ThrowsAsync<ConflictException>(() => AsStaff(() => SendAsync(new ReceiveReturnCommand(order, parcel))));
    }

    [Fact]
    public async Task The_return_shows_on_the_buyer_s_order_and_the_seller_s_sale()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);

        var sale = await As(alice, () => SendAsync(new GetMySaleQuery(order)));
        Assert.Equal("Requested", sale.Return?.Status);

        await using var scope = _fixture.NewScope();
        var mine = await scope.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdForUserAsync(order, buyer);
        Assert.Equal("Requested", OrderMapping.ToDetail(mine!).Shipments!.Single(s => s.Id == parcel).Return?.Status);
    }

    [Fact]
    public async Task Staff_see_the_escalated_ones_waiting()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "No")));
        await As(buyer, () => SendAsync(new EscalateReturnCommand(order, parcel)));

        var page = await AsStaff(() => SendAsync(new GetReturnsQuery("Escalated", 1, 50)));
        Assert.Contains(page.Items, r => r.ShipmentId == parcel);
        Assert.All(page.Items, r => Assert.Equal("Escalated", r.Status));
    }

    [Fact]
    public async Task Every_step_is_on_the_record()
    {
        var alice = Guid.CreateVersion7();
        var (order, _, _) = await SentBackAsync(alice);
        await As(alice, () => SendAsync(new ReceiveSaleReturnCommand(order)));

        var actions = _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == order.ToString() && e.Action.StartsWith("Return")).Select(e => e.Action).ToList();
        Assert.Equal(["ReturnRequested", "ReturnAccepted", "ReturnSentBack", "ReturnReceived"], actions);
    }

    // ------------------------------------------------------------------ US4: money - a hold, never a debt

    [Fact]
    public async Task An_open_return_holds_the_money_past_the_window()
    {
        var alice = Guid.CreateVersion7();
        var (order, _, _) = await RequestedAsync(alice);
        await ReturnWindow.PassAsync(_fixture, order);

        var balance = Assert.Single(await As(alice, () => SendAsync(new GetMyBalanceQuery())));
        Assert.Equal(0m, balance.Due);
        await Assert.ThrowsAsync<ConflictException>(() => AsStaff(() => SendAsync(new RecordPayoutCommand(alice, "VND"))));
    }

    [Fact]
    public async Task A_final_refusal_releases_the_money()
    {
        var alice = Guid.CreateVersion7();
        var (order, buyer, parcel) = await RequestedAsync(alice);
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "No")));
        await As(buyer, () => SendAsync(new EscalateReturnCommand(order, parcel)));
        await AsStaff(() => SendAsync(new DecideReturnCommand(order, parcel, false, "Upheld")));
        await ReturnWindow.PassAsync(_fixture, order);

        var payout = await AsStaff(() => SendAsync(new RecordPayoutCommand(alice, "VND")));
        Assert.Equal(1, payout.PartCount);
    }

    [Fact]
    public async Task A_refusal_left_alone_releases_the_money_once_its_window_is_over()
    {
        var alice = Guid.CreateVersion7();
        var (order, _, parcel) = await RequestedAsync(alice);
        await As(alice, () => SendAsync(new DecideSaleReturnCommand(order, false, "No")));
        await ReturnWindow.PassAsync(_fixture, order);
        await Assert.ThrowsAsync<ConflictException>(() => AsStaff(() => SendAsync(new RecordPayoutCommand(alice, "VND"))));

        await AgeDecisionAsync(parcel);

        Assert.Equal(1, (await AsStaff(() => SendAsync(new RecordPayoutCommand(alice, "VND")))).PartCount);
    }

    [Fact]
    public async Task A_returned_parcel_is_no_money_at_all()
    {
        var alice = Guid.CreateVersion7();
        var (order, _, _) = await SentBackAsync(alice);
        await As(alice, () => SendAsync(new ReceiveSaleReturnCommand(order)));
        await ReturnWindow.PassAsync(_fixture, order);

        Assert.Empty(await As(alice, () => SendAsync(new GetMyBalanceQuery())));
        await Assert.ThrowsAsync<ConflictException>(() => AsStaff(() => SendAsync(new RecordPayoutCommand(alice, "VND"))));
    }

    /// <summary>The claim is SQL and the balance is LINQ: the two must say the same parts are due.</summary>
    [Fact]
    public async Task The_payout_claims_exactly_what_the_balance_calls_due()
    {
        var alice = Guid.CreateVersion7();
        var (plain, _, _) = await DeliveredAsync(alice);
        var (held, _, _) = await RequestedAsync(alice);
        var (returned, _, _) = await SentBackAsync(alice);
        await As(alice, () => SendAsync(new ReceiveSaleReturnCommand(returned)));
        foreach (var o in new[] { plain, held, returned })
            await ReturnWindow.PassAsync(_fixture, o);

        var due = Assert.Single(await As(alice, () => SendAsync(new GetMyBalanceQuery()))).Due;
        var payout = await AsStaff(() => SendAsync(new RecordPayoutCommand(alice, "VND")));

        Assert.Equal(due, payout.Amount);
        Assert.Equal(1, payout.PartCount);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(Guid Order, Guid Buyer, Guid Parcel)> DeliveredAsync(Guid? seller, int quantity = 1)
    {
        var (order, buyer) = await PaidCheckoutAsync(seller, quantity);
        await ShipAsync(order, seller);
        var parcel = (await PartAsync(order, seller)).Id;
        await As(buyer, () => SendAsync(new ConfirmDeliveryCommand(order, parcel)));
        return (order, buyer, parcel);
    }

    private async Task<(Guid Order, Guid Buyer, Guid Parcel)> RequestedAsync(Guid? seller, int quantity = 1)
    {
        var (order, buyer, parcel) = await DeliveredAsync(seller, quantity);
        await As(buyer, () => SendAsync(new RequestReturnCommand(order, parcel, "Broken on arrival")));
        return (order, buyer, parcel);
    }

    private async Task<(Guid Order, Guid Buyer, Guid Parcel)> AcceptedAsync(Guid? seller, int quantity = 1)
    {
        var (order, buyer, parcel) = await RequestedAsync(seller, quantity);
        if (seller is { } s)
            await As(s, () => SendAsync(new DecideSaleReturnCommand(order, true, null)));
        else
            await AsStaff(() => SendAsync(new DecideReturnCommand(order, parcel, true, null)));
        return (order, buyer, parcel);
    }

    private async Task<(Guid Order, Guid Buyer, Guid Parcel)> SentBackAsync(Guid? seller, int quantity = 1)
    {
        var (order, buyer, parcel) = await AcceptedAsync(seller, quantity);
        await As(buyer, () => SendAsync(new SendReturnBackCommand(order, parcel, "VNPOST-BACK")));
        return (order, buyer, parcel);
    }

    /// <summary>Moves the return's decision 8 days back - past the window the buyer has to act on it.</summary>
    private async Task AgeDecisionAsync(Guid parcel)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().ParcelReturns.Where(r => r.ShipmentId == parcel)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.DecidedAt, DateTime.UtcNow.AddDays(-8)));
    }

    private async Task<int> CountAsync(Guid parcel)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().ParcelReturns.CountAsync(r => r.ShipmentId == parcel);
    }

    private List<UserNotificationRequested> Told(Guid order) =>
        _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Data.TryGetValue("orderId", out var id) && id == order.ToString()).ToList();

    private async Task<T> As<T>(Guid user, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = user;
        return await body();
    }

    private Task<T> AsStaff<T>(Func<Task<T>> body) => As(Guid.CreateVersion7(), body);

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<(Guid Order, Guid Customer)> PaidCheckoutAsync(Guid? seller, int quantity = 1)
    {
        var customer = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = customer;
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        _fixture.Checkout.Prices[variant] = new CatalogPrice(
            product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller, null);
        _fixture.Checkout.Cart = [new CartItem(product, quantity, variant)];
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

    private async Task<OrderShipment> PartAsync(Guid order, Guid? seller)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.AsNoTracking().SingleAsync(s => s.OrderId == order && s.SellerId == seller);
    }
}
