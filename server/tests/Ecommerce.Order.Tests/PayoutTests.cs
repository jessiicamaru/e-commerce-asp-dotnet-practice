using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyBalance;
using Ecommerce.Order.Application.Orders.Queries.GetMyPayouts;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Application.Orders.Queries.GetMySales;
using Ecommerce.Order.Application.Orders.Queries.GetPayoutsDue;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What the shop owes each seller (specs/037): terms frozen at checkout, a balance per currency, and a
/// payout that settles each part exactly once.
/// </summary>
/// <remarks>
/// Every test mints its own sellers, because the database is shared by the collection and "what is due
/// to this seller" means nothing unless nobody else can have sold as them.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class PayoutTests
{
    private readonly OrderTestFixture _fixture;

    /// <summary>
    /// The fixture is shared by the collection and other classes change its currency, so each test here
    /// starts from dong and 10% rather than from whatever the previous test left.
    /// </summary>
    public PayoutTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    // ------------------------------------------------------------------ US1: terms frozen at checkout

    [Fact]
    public async Task Checkout_records_the_rate_and_what_each_part_earns()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();

        // 30,000 ₫ of delivery over three parts; 10% commission.
        var order = await CheckoutAsync((alice, 2), (bob, 1), (null, 1));

        var row = await OrderSeed.ReadAsync(_fixture, order);
        Assert.Equal(0.10m, row.CommissionRate);

        var parts = await PartsAsync(order);
        var hers = parts.Single(p => p.SellerId == alice);
        Assert.Equal(2000m, hers.GoodsTotal);
        Assert.Equal(200m, hers.Commission);
        Assert.Equal(10000m, hers.ShippingShare);

        // The shop does not charge itself commission, and keeps its share of delivery.
        var shop = parts.Single(p => p.SellerId == null);
        Assert.Equal(0m, shop.Commission);
        Assert.Equal(10000m, shop.ShippingShare);
    }

    /// <summary>SC-001: to the smallest unit, even when the charge does not divide.</summary>
    [Fact]
    public async Task The_delivery_shares_sum_exactly_to_the_delivery_charge()
    {
        _fixture.Currency = new Currency("USD", 2);
        try
        {
            // $5.00 over three parts is 1.68 / 1.66 / 1.66.
            var order = await CheckoutAsync((Guid.CreateVersion7(), 1), (Guid.CreateVersion7(), 1), (null, 1));

            var row = await OrderSeed.ReadAsync(_fixture, order);
            var shares = (await PartsAsync(order)).Select(p => p.ShippingShare!.Value).ToList();

            Assert.Equal(5.00m, row.ShippingPrice);
            Assert.Equal(row.ShippingPrice, shares.Sum());
            Assert.Equal(new[] { 1.66m, 1.66m, 1.68m }, shares.Order());
        }
        finally
        {
            _fixture.Currency = new Currency("VND", 0);
        }
    }

    /// <summary>SC-002: the rate is an agreement made at the sale, not a view of today's configuration.</summary>
    [Fact]
    public async Task A_rate_changed_afterwards_leaves_an_existing_sale_unchanged()
    {
        var alice = Guid.CreateVersion7();
        var before = await PaidCheckoutAsync((alice, 1));

        _fixture.Commission.Current = 0.25m;
        try
        {
            var after = await PaidCheckoutAsync((alice, 1));

            var old = await AsSeller(alice, () => SendAsync(new GetMySaleQuery(before)));
            var @new = await AsSeller(alice, () => SendAsync(new GetMySaleQuery(after)));

            Assert.Equal(100m, old.Commission);   // 10% of 1,000
            Assert.Equal(250m, @new.Commission);  // proves the rate IS read, and changed
        }
        finally
        {
            _fixture.Commission.Current = 0.10m;
        }
    }

    [Fact]
    public async Task A_seller_s_sales_carry_what_they_earn()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidCheckoutAsync((alice, 3), (Guid.CreateVersion7(), 1));

        var detail = await AsSeller(alice, () => SendAsync(new GetMySaleQuery(order)));
        Assert.Equal(3000m, detail.GoodsTotal);
        Assert.Equal(300m, detail.Commission);
        Assert.Equal(15000m, detail.ShippingShare);
        Assert.Equal(17700m, detail.Payout);
        Assert.False(detail.PaidOut);

        var row = Assert.Single((await AsSeller(alice, () => SendAsync(new GetMySalesQuery()))).Items);
        Assert.Equal(17700m, row.Payout);
        Assert.Equal(300m, row.Commission);
    }

    /// <summary>research D6: terms nobody recorded are not invented now.</summary>
    [Fact]
    public async Task A_sale_from_before_this_has_no_earnings_rather_than_invented_ones()
    {
        var alice = Guid.CreateVersion7();
        var order = await LegacyOrderAsync(OrderStatus.Paid, alice);

        var detail = await AsSeller(alice, () => SendAsync(new GetMySaleQuery(order)));

        Assert.Null(detail.GoodsTotal);
        Assert.Null(detail.Commission);
        Assert.Null(detail.ShippingShare);
        Assert.Null(detail.Payout);
    }

    // ------------------------------------------------------------------ US2: balance and history

    [Fact]
    public async Task The_balance_moves_from_on_the_way_to_due_to_paid_out()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidCheckoutAsync((alice, 1), (Guid.CreateVersion7(), 1));
        const decimal owed = 1000m - 100m + 15000m;

        Assert.Equal(new BalanceResponse("VND", owed, 0m, 0m), Assert.Single(await BalanceAsync(alice)));

        await ShipAsync(order, alice);
        Assert.Equal(new BalanceResponse("VND", 0m, owed, 0m), Assert.Single(await BalanceAsync(alice)));

        await AsAdmin(() => SendAsync(new RecordPayoutCommand(alice, "VND")));
        Assert.Equal(new BalanceResponse("VND", 0m, 0m, owed), Assert.Single(await BalanceAsync(alice)));

        var detail = await AsSeller(alice, () => SendAsync(new GetMySaleQuery(order)));
        Assert.True(detail.PaidOut);
    }

    /// <summary>
    /// ⚠️ A failed order's parts EXIST - checkout writes them - so nothing but the status filter keeps a
    /// declined payment out of a seller's balance.
    /// </summary>
    [Fact]
    public async Task Orders_that_failed_or_are_still_settling_count_nowhere()
    {
        var alice = Guid.CreateVersion7();
        var settling = await CheckoutAsync((alice, 1));
        var failed = await CheckoutAsync((alice, 1));
        await SettleAsync(failed, OrderStatus.Failed);

        Assert.Empty(await BalanceAsync(alice));

        // Even shipped by hand, straight in the table, a failed order is never due.
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
                .Where(s => s.OrderId == failed || s.OrderId == settling)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, ShipmentStatus.Shipped));
        }

        Assert.Empty(await BalanceAsync(alice));
        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin(() => SendAsync(new RecordPayoutCommand(alice, "VND"))));
    }

    [Fact]
    public async Task A_seller_sees_only_their_own_payouts()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await PaidCheckoutAsync((alice, 1), (bob, 1));
        await ShipAsync(order, alice);
        await ShipAsync(order, bob);
        var hers = await AsAdmin(() => SendAsync(new RecordPayoutCommand(alice, "VND")));
        await AsAdmin(() => SendAsync(new RecordPayoutCommand(bob, "VND")));

        var page = await AsSeller(alice, () => SendAsync(new GetMyPayoutsQuery()));

        var only = Assert.Single(page.Items);
        Assert.Equal(hers.Id, only.Id);
        Assert.Equal(1, page.TotalCount);
        Assert.Empty(await BalanceAsync(Guid.CreateVersion7()));
    }

    // ------------------------------------------------------------------ US3: an administrator settles

    [Fact]
    public async Task The_due_list_names_each_seller_and_what_is_due()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidCheckoutAsync((alice, 2), (null, 1));
        await ShipAsync(order, alice);

        var due = await AsAdmin(() => SendAsync(new GetPayoutsDueQuery()));

        var row = Assert.Single(due, d => d.SellerId == alice);
        Assert.Equal("Shop " + alice.ToString("N")[..6], row.SellerName);
        Assert.Equal("VND", row.Currency);
        Assert.Equal(2000m - 200m + 15000m, row.Due);
        Assert.Equal(1, row.Parts);
        // The shop is never owed anything, so it is never on the list.
        Assert.DoesNotContain(due, d => d.SellerId == Guid.Empty);
    }

    [Fact]
    public async Task A_payout_covers_every_part_due_and_records_who_made_it()
    {
        var alice = Guid.CreateVersion7();
        var admin = Guid.CreateVersion7();
        var first = await PaidCheckoutAsync((alice, 1), (Guid.CreateVersion7(), 1));   // 900 + 15,000
        var second = await PaidCheckoutAsync((alice, 2));                               // 1,800 + 30,000
        var notYet = await PaidCheckoutAsync((alice, 5));                               // not shipped
        await ShipAsync(first, alice);
        await ShipAsync(second, alice);

        _fixture.CurrentUser.Id = admin;
        var payout = await SendAsync(new RecordPayoutCommand(alice, "VND"));

        Assert.Equal(alice, payout.SellerId);
        Assert.Equal(15900m + 31800m, payout.Amount);
        Assert.Equal(2, payout.PartCount);

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Equal(admin, (await context.Payouts.SingleAsync(p => p.Id == payout.Id)).RecordedBy);

        var claimed = await context.OrderShipments
            .Where(s => s.SellerId == alice)
            .ToDictionaryAsync(s => s.OrderId, s => s.PayoutId);
        Assert.Equal(payout.Id, claimed[first]);
        Assert.Equal(payout.Id, claimed[second]);
        Assert.Null(claimed[notYet]);
    }

    [Fact]
    public async Task Nothing_due_is_refused_and_leaves_nothing_behind()
    {
        var alice = Guid.CreateVersion7();
        await PaidCheckoutAsync((alice, 1));   // paid, not shipped

        var refused = await Assert.ThrowsAsync<ConflictException>(
            () => AsAdmin(() => SendAsync(new RecordPayoutCommand(alice, "VND"))));

        Assert.Equal("Nothing is due to this seller in VND.", refused.Message);
        await using var scope = _fixture.NewScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Payouts.AnyAsync(p => p.SellerId == alice));
    }

    /// <summary>
    /// ⚠️ SC-003. Every attempt runs at once; the guarded claim is what makes exactly one of them pay and
    /// the rest find nothing due. Summing first and claiming second would pay the same parts twice.
    /// </summary>
    [Fact]
    public async Task Simultaneous_payouts_pay_each_part_exactly_once()
    {
        var alice = Guid.CreateVersion7();
        for (var i = 0; i < 3; i++)
        {
            await ShipAsync(await PaidCheckoutAsync((alice, 1)), alice);
        }

        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                return await SendAsync(new RecordPayoutCommand(alice, "VND"));
            }
            catch (ConflictException)
            {
                return null;
            }
        }));

        var paid = Assert.Single(attempts, a => a is not null)!;
        Assert.Equal(3, paid.PartCount);
        Assert.Equal(3 * (900m + 30000m), paid.Amount);

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Equal(1, await context.Payouts.CountAsync(p => p.SellerId == alice));
        Assert.All(await context.OrderShipments.Where(s => s.SellerId == alice).ToListAsync(),
            s => Assert.Equal(paid.Id, s.PayoutId));
    }

    [Fact]
    public async Task A_payout_in_one_currency_leaves_the_other_due()
    {
        var alice = Guid.CreateVersion7();
        await ShipAsync(await PaidCheckoutAsync((alice, 1)), alice);
        _fixture.Currency = new Currency("USD", 2);
        try
        {
            await ShipAsync(await PaidCheckoutAsync((alice, 1)), alice);
        }
        finally
        {
            _fixture.Currency = new Currency("VND", 0);
        }

        await AsAdmin(() => SendAsync(new RecordPayoutCommand(alice, "VND")));

        var balance = (await BalanceAsync(alice)).ToDictionary(b => b.Currency);
        Assert.Equal(0m, balance["VND"].Due);
        Assert.True(balance["USD"].Due > 0m);
    }

    /// <summary>research D6: a part with no recorded terms is never paid, even once shipped.</summary>
    [Fact]
    public async Task A_part_from_before_this_is_never_paid_out()
    {
        var alice = Guid.CreateVersion7();
        var order = await LegacyOrderAsync(OrderStatus.Paid, alice);
        await ShipAsync(order, alice);

        Assert.Empty(await BalanceAsync(alice));
        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin(() => SendAsync(new RecordPayoutCommand(alice, "VND"))));
    }

    [Fact]
    public async Task A_payout_request_without_a_seller_or_currency_is_invalid()
    {
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => AsAdmin(() => SendAsync(new RecordPayoutCommand(Guid.Empty, "VND"))));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => AsAdmin(() => SendAsync(new RecordPayoutCommand(Guid.CreateVersion7(), ""))));
    }

    // ------------------------------------------------------------------ helpers

    private Task<List<BalanceResponse>> BalanceAsync(Guid seller) =>
        AsSeller(seller, () => SendAsync(new GetMyBalanceQuery()));

    private async Task<T> AsSeller<T>(Guid seller, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = seller;
        return await body();
    }

    private Task<T> AsAdmin<T>(Func<Task<T>> body) => AsSeller(Guid.CreateVersion7(), body);

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Checks out one line per (seller, quantity) at 1,000 each, in the fixture's currency.</summary>
    private async Task<Guid> CheckoutAsync(params (Guid? Seller, int Quantity)[] lines)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        var cart = new List<CartItem>();
        foreach (var (seller, quantity) in lines)
        {
            var product = Guid.CreateVersion7();
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "",
                _fixture.Currency.Code, seller, seller is { } s ? "Shop " + s.ToString("N")[..6] : null);
            cart.Add(new CartItem(product, quantity, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        return (await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId;
    }

    private async Task<Guid> PaidCheckoutAsync(params (Guid? Seller, int Quantity)[] lines)
    {
        var order = await CheckoutAsync(lines);
        await SettleAsync(order, OrderStatus.Paid);
        return order;
    }

    private async Task SettleAsync(Guid order, OrderStatus status)
    {
        await using var scope = _fixture.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
            .TrySettleAsync(order, status, null, DateTime.UtcNow));
    }

    /// <summary>
    /// Ships the part AND has it arrive: since specs/040 money is due for a DELIVERED parcel, and these tests
    /// are about what happens once it is due. That a shipped-only parcel is not due is DeliveryTests' job.
    /// </summary>
    private async Task ShipAsync(Guid order, Guid? seller)
    {
        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        Assert.Equal(ShipmentMoveOutcome.Moved, (await repository.TryMoveShipmentAsync(
            order, seller, ShipmentStatus.Pending, ShipmentStatus.Preparing, null, DateTime.UtcNow)).Outcome);
        Assert.Equal(ShipmentMoveOutcome.Moved, (await repository.TryMoveShipmentAsync(
            order, seller, ShipmentStatus.Preparing, ShipmentStatus.Shipped, "VNPOST", DateTime.UtcNow)).Outcome);

        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments
            .Where(s => s.OrderId == order && s.SellerId == seller)
            .ExecuteUpdateAsync(x => x
                .SetProperty(s => s.DeliveredAt, DateTime.UtcNow)
                .SetProperty(s => s.DeliveryConfirmedBy, "Customer"));
    }

    /// <summary>An order as an image from before this feature wrote it: no rate, no parts, no terms.</summary>
    private async Task<Guid> LegacyOrderAsync(OrderStatus status, Guid seller)
    {
        var orderId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            context.Orders.Add(new Domain.Entities.Order
            {
                Id = orderId,
                UserId = Guid.CreateVersion7(),
                TotalAmount = 1000m,
                Status = status,
                Currency = "VND",
                Language = "vi",
                ShipTo = new ShippingAddress
                {
                    RecipientName = Home.RecipientName, Line1 = Home.Line1, City = Home.City,
                    PostalCode = Home.PostalCode, Country = Home.Country, Phone = Home.Phone
                },
                Items =
                [
                    new OrderItem
                    {
                        Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(),
                        ProductName = "Old camera", SellerId = seller, Quantity = 1, UnitPrice = 1000m
                    }
                ]
            });
            await context.SaveChangesAsync();
        }

        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOrderRepository>().EnsureShipmentsAsync(orderId);
        }

        return orderId;
    }

    private async Task<List<OrderShipment>> PartsAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.AsNoTracking().Where(s => s.OrderId == order).ToListAsync();
    }
}
