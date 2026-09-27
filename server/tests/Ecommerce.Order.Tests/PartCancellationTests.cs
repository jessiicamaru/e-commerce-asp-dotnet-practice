using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.CancelOrder;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyBalance;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A seller cancels the part of an order they cannot fulfil (#211, specs/104): the part is refunded and restocked, the
/// rest of the order goes on, and the last part cancels the order - never refunding anything twice.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class PartCancellationTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private static readonly AddressCopy Home =
        new("Lan Pham", "12 Hang Bac", null, "Hanoi", null, "100000", "VN", "+84 90 000 0000");

    [Fact]
    public async Task A_seller_cancels_their_part_and_the_rest_of_the_order_goes_on()
    {
        var (alice, bob) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var order = await PaidOrderAsync(alice, bob);

        var sale = await AsSeller(alice, () => SendAsync(new CancelSalePartCommand(order, "  Out of stock  ")));

        Assert.Equal(("Cancelled", "Out of stock", "Seller"), (sale.Status, sale.CancelReason, sale.CancelledBy));
        Assert.Null(sale.Payout);   // earns nothing
        var part = Assert.Single(PartEvents(order));
        Assert.Equal(125m, part.Amount);   // 100 less 10 of discounts, plus 35 tax - the buyer's price for it
        Assert.Equal("Seller", part.CancelledBy);
        Assert.Single(part.VariantIds);
        Assert.Empty(WholeEvents(order));
        Assert.Equal(OrderStatus.Paid, (await ReadAsync(order)).Status);

        var told = Assert.Single(Notices(order));
        Assert.Equal(("Out of stock", "Shop A"), (told.Data["reason"], told.Data["shop"]));
        Assert.Empty(NotificationContract.Problems(told.Kind, told.Data));

        // The other seller ships, and the order does not wait for the cancelled part.
        await AsSeller(bob, () => SendAsync(new PrepareMySaleCommand(order)));
        await AsSeller(bob, () => SendAsync(new ShipMySaleCommand(order, "VN-B")));
        Assert.Equal(OrderStatus.Shipped, (await ReadAsync(order)).Status);
        Assert.Equal(125m, (await PartAsync(order, alice)).CancelRefund);
    }

    [Fact]
    public async Task A_shipped_part_or_somebody_elses_cannot_be_cancelled_and_a_repeat_changes_nothing()
    {
        var (alice, bob) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var order = await PaidOrderAsync(alice, bob);
        await AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await AsSeller(alice, () => SendAsync(new ShipMySaleCommand(order, "VN-A")));

        await Assert.ThrowsAsync<ConflictException>(() => AsSeller(alice, () => SendAsync(new CancelSalePartCommand(order, "Too late"))));
        var stranger = await Assert.ThrowsAsync<NotFoundException>(() =>
            AsSeller(Guid.CreateVersion7(), () => SendAsync(new CancelSalePartCommand(order, "Not mine"))));
        Assert.Equal(Sales.NotFound, stranger.Message);
        await Assert.ThrowsAsync<ValidationException>(() => AsSeller(bob, () => SendAsync(new CancelSalePartCommand(order, " "))));

        await AsSeller(bob, () => SendAsync(new CancelSalePartCommand(order, "Damaged")));
        await AsSeller(bob, () => SendAsync(new CancelSalePartCommand(order, "Damaged")));
        Assert.Single(PartEvents(order));
        // A cancelled part never moves again.
        await Assert.ThrowsAsync<ConflictException>(() => AsSeller(bob, () => SendAsync(new PrepareMySaleCommand(order))));
    }

    [Fact]
    public async Task The_last_part_cancels_the_order()
    {
        var solo = Guid.CreateVersion7();
        var single = await PaidOrderAsync(solo);
        await AsSeller(solo, () => SendAsync(new CancelSalePartCommand(single, "Out of stock")));

        Assert.Equal((OrderStatus.Cancelled, "Seller"), await StatusAsync(single));
        Assert.Equal("Seller", Assert.Single(WholeEvents(single)).CancelledBy);
        Assert.Empty(PartEvents(single));
        Assert.Null((await PartAsync(single, solo)).CancelRefund);   // refunded through the whole order

        var (alice, bob) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var two = await PaidOrderAsync(alice, bob);
        await AsSeller(alice, () => SendAsync(new CancelSalePartCommand(two, "Out of stock")));
        await AsSeller(bob, () => SendAsync(new CancelSalePartCommand(two, "Out of stock too")));
        Assert.Equal(OrderStatus.Cancelled, (await ReadAsync(two)).Status);
        Assert.Single(PartEvents(two));
        Assert.Single(WholeEvents(two));
    }

    /// <summary>
    /// Two sellers cancelling the last two parts at the same moment: under the order's lock exactly one of them sees it
    /// is the last and cancels the order. Without it both see "another part remains" and the order stays Paid with
    /// nothing left to ship. Fifteen orders, so the window is hit rather than hoped for.
    /// </summary>
    [Fact]
    public async Task Two_sellers_cancelling_at_once_cancel_the_order_exactly_once()
    {
        var pairs = new List<(Guid Order, Guid A, Guid B)>();
        for (var i = 0; i < 15; i++)
        {
            var (a, b) = (Guid.CreateVersion7(), Guid.CreateVersion7());
            pairs.Add((await PaidOrderAsync(a, b), a, b));
        }

        await Task.WhenAll(pairs.SelectMany(p => new[] { CancelAsync(p.Order, p.A), CancelAsync(p.Order, p.B) }));

        foreach (var (order, _, _) in pairs)
        {
            Assert.Equal(OrderStatus.Cancelled, (await ReadAsync(order)).Status);
            Assert.Single(WholeEvents(order));
            Assert.Single(PartEvents(order));
        }
    }

    [Fact]
    public async Task A_cancelled_part_is_no_money_and_its_sellers_voucher_comes_back()
    {
        var (alice, bob) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var order = await PaidCheckoutAsync(alice, bob);
        var (aliceVoucher, bobVoucher) = (await RedeemedAsync(order, alice), await RedeemedAsync(order, bob));
        Assert.NotEmpty(await BalanceAsync(alice));

        await AsSeller(alice, () => SendAsync(new CancelSalePartCommand(order, "Out of stock")));

        Assert.Empty(await BalanceAsync(alice));
        Assert.NotEmpty(await BalanceAsync(bob));
        Assert.Equal((0, true), await VoucherAsync(aliceVoucher));   // given back
        Assert.Equal((1, false), await VoucherAsync(bobVoucher));    // the other seller's stays used
    }

    [Fact]
    public async Task Staff_cancel_the_shops_own_part_only()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(null, alice);
        _fixture.CurrentUser.Id = Guid.CreateVersion7();

        var detail = await SendAsync(new CancelShopPartCommand(order, "Discontinued"));

        var shop = Assert.Single(detail.Shipments!, s => s.IsShop);
        Assert.Equal(("Cancelled", "Staff", "Discontinued"), (shop.Status, shop.CancelledBy, shop.CancelReason));
        Assert.Equal("Paid", detail.Status);
        Assert.False(Assert.Single(Notices(order)).Data.ContainsKey("shop"));

        var onlySellers = await PaidOrderAsync(alice);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new CancelShopPartCommand(onlySellers, "No shop part")));
    }

    // ------------------------------------------------------------------ helpers

    private List<OrderPartCancelledEvent> PartEvents(Guid order) =>
        _fixture.Harness.Published.Select<OrderPartCancelledEvent>().Select(x => x.Context.Message).Where(m => m.OrderId == order).ToList();

    private List<OrderCancelledEvent> WholeEvents(Guid order) =>
        _fixture.Harness.Published.Select<OrderCancelledEvent>().Select(x => x.Context.Message).Where(m => m.OrderId == order).ToList();

    private List<UserNotificationRequested> Notices(Guid order) =>
        _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Kind == NotificationKind.PartCancelled && n.Data.GetValueOrDefault("orderId") == order.ToString()).ToList();

    private async Task CancelAsync(Guid order, Guid seller)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<IOrderRepository>().TryCancelPartAsync(
            order, seller, "Out of stock", PartCancellation.BySeller, DateTime.UtcNow, async (part, ct) =>
            {
                var publish = scope.ServiceProvider.GetRequiredService<MassTransit.IPublishEndpoint>();
                if (part.Last) await publish.Publish(new OrderCancelledEvent(order, DateTime.UtcNow, "Seller"), ct);
                else await publish.Publish(new OrderPartCancelledEvent(order, part.PartId, part.VariantIds, part.Refund, part.Currency, DateTime.UtcNow, "Seller"), ct);
            });
    }

    private async Task<T> AsSeller<T>(Guid seller, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = seller;
        return await body();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<Domain.Entities.Order> ReadAsync(Guid order) => await OrderSeed.ReadAsync(_fixture, order);

    private async Task<(OrderStatus, string?)> StatusAsync(Guid order)
    {
        var read = await ReadAsync(order);
        return (read.Status, read.CancelledBy);
    }

    private async Task<OrderShipment> PartAsync(Guid order, Guid? seller)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments.AsNoTracking()
            .SingleAsync(s => s.OrderId == order && s.SellerId == seller);
    }

    private async Task<List<BalanceResponse>> BalanceAsync(Guid seller)
    {
        _fixture.CurrentUser.Id = seller;
        return await SendAsync(new GetMyBalanceQuery());
    }

    /// <summary>
    /// A paid order with one line per seller (null = the shop): 100 a unit, a shop discount of 6 and a platform one of 4,
    /// 35 of tax - so the buyer paid 125 for each line - and its parts in place.
    /// </summary>
    private async Task<Guid> PaidOrderAsync(params Guid?[] sellers)
    {
        var orderId = Guid.CreateVersion7();
        var at = DateTime.UtcNow.AddMinutes(-5);
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            context.Orders.Add(new Domain.Entities.Order
            {
                Id = orderId,
                UserId = Guid.CreateVersion7(),
                TotalAmount = 125m * sellers.Length,
                Status = OrderStatus.Paid,
                CreatedAt = at,
                UpdatedAt = at,
                Currency = "VND",
                Language = "vi",
                ShipTo = new ShippingAddress
                {
                    RecipientName = Home.RecipientName, Line1 = Home.Line1, City = Home.City,
                    PostalCode = Home.PostalCode, Country = Home.Country, Phone = Home.Phone
                },
                Items = sellers.Select((seller, i) => new OrderItem
                {
                    Id = Guid.CreateVersion7(),
                    OrderId = orderId,
                    ProductId = Guid.CreateVersion7(),
                    VariantId = Guid.CreateVersion7(),
                    ProductName = "Camera",
                    SellerId = seller,
                    SellerName = seller is null ? null : "Shop " + (char)('A' + i),
                    Quantity = 1,
                    UnitPrice = 100m,
                    ShopDiscount = 6m,
                    PlatformDiscount = 4m,
                    TaxAmount = 35m,
                }).ToList()
            });
            await context.SaveChangesAsync();
        }

        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOrderRepository>().EnsureShipmentsAsync(orderId);
        }

        return orderId;
    }

    /// <summary>A real checkout - terms recorded, so the parts are money - settled to Paid.</summary>
    private async Task<Guid> PaidCheckoutAsync(params Guid[] sellers)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        var cart = new List<CartItem>();
        foreach (var seller in sellers)
        {
            var (product, variant) = (Guid.CreateVersion7(), Guid.CreateVersion7());
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "",
                _fixture.Currency.Code, seller, "Shop " + seller.ToString("N")[..6]);
            cart.Add(new CartItem(product, 1, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        var order = (await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId;
        await using var scope = _fixture.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderRepository>().TrySettleAsync(order, OrderStatus.Paid, null, DateTime.UtcNow));
        return order;
    }

    /// <summary>A voucher of this seller's, used once by this order - as checkout records it (specs/069).</summary>
    private async Task<Guid> RedeemedAsync(Guid order, Guid seller)
    {
        var voucher = new Voucher
        {
            Id = Guid.CreateVersion7(),
            Code = $"V{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            SellerId = seller,
            Name = "Seller voucher",
            Benefit = VoucherBenefit.Percent,
            Percent = 5m,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            UsedCount = 1,
            CreatedBy = seller,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        context.Vouchers.Add(voucher);
        context.VoucherRedemptions.Add(new VoucherRedemption
        {
            Id = Guid.CreateVersion7(),
            VoucherId = voucher.Id,
            OrderId = order,
            CustomerId = Guid.CreateVersion7(),
            Code = voucher.Code,
            Name = voucher.Name,
            SellerId = seller,
            Benefit = voucher.Benefit,
            Amount = 50m,
            Currency = "VND",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
        return voucher.Id;
    }

    private async Task<(int UsedCount, bool Released)> VoucherAsync(Guid voucherId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var used = await context.Vouchers.AsNoTracking().Where(v => v.Id == voucherId).Select(v => v.UsedCount).SingleAsync();
        var released = await context.VoucherRedemptions.AsNoTracking().Where(r => r.VoucherId == voucherId).Select(r => r.ReleasedAt != null).SingleAsync();
        return (used, released);
    }
}
