using Ecommerce.Contracts.Activity;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.CancelOrder;
using Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;
using Ecommerce.Order.Application.Orders.Commands.CorrectTracking;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Common;
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
/// A mistyped tracking reference is corrected (#212, specs/105): while the parcel is on its way, by whoever sent it, with
/// the buyer told and both references on the record - and the parcel's shipping time never moves.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class TrackingCorrectionTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task A_seller_corrects_their_shipped_part_and_the_buyer_is_told()
    {
        var alice = Guid.CreateVersion7();
        var order = await ShippedAsync([alice], alice, "VN-1234");
        var shippedAt = (await PartAsync(order, alice)).ShippedAt;

        var sale = await AsSeller(alice, () => SendAsync(new CorrectSaleTrackingCommand(order, "  VN-1243  ")));

        Assert.Equal("VN-1243", sale.TrackingReference);
        var part = await PartAsync(order, alice);
        Assert.Equal(("VN-1243", shippedAt), (part.TrackingReference, part.ShippedAt));   // the clock stands
        Assert.Equal("VN-1243", (await OrderSeed.ReadAsync(_fixture, order)).TrackingReference);   // one part: the order's too

        var told = Assert.Single(Notices(order));
        Assert.Equal("VN-1243", told.Data["tracking"]);
        Assert.Empty(NotificationContract.Problems(told.Kind, told.Data));
        var entry = Assert.Single(Audited(order));
        Assert.Contains("VN-1234", entry.Before);
        Assert.Contains("VN-1243", entry.After);

        // The same reference again: nothing changes, nobody is told twice.
        await AsSeller(alice, () => SendAsync(new CorrectSaleTrackingCommand(order, "VN-1243")));
        Assert.Single(Notices(order));
        Assert.Single(Audited(order));
    }

    [Fact]
    public async Task Not_shipped_delivered_cancelled_or_somebody_elses_is_refused()
    {
        var (alice, bob) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var order = await ShippedAsync([alice, bob], alice, "VN-A");

        await Assert.ThrowsAsync<ConflictException>(() => AsSeller(bob, () => SendAsync(new CorrectSaleTrackingCommand(order, "VN-B"))));
        var stranger = await Assert.ThrowsAsync<NotFoundException>(() =>
            AsSeller(Guid.CreateVersion7(), () => SendAsync(new CorrectSaleTrackingCommand(order, "VN-X"))));
        Assert.Equal(Sales.NotFound, stranger.Message);
        await Assert.ThrowsAsync<ValidationException>(() => AsSeller(alice, () => SendAsync(new CorrectSaleTrackingCommand(order, " "))));

        await AsSeller(bob, () => SendAsync(new CancelSalePartCommand(order, "Out of stock")));
        await Assert.ThrowsAsync<ConflictException>(() => AsSeller(bob, () => SendAsync(new CorrectSaleTrackingCommand(order, "VN-B"))));

        // Delivered: nothing left to track.
        _fixture.CurrentUser.Id = (await OrderSeed.ReadAsync(_fixture, order)).UserId;
        await SendAsync(new ConfirmDeliveryCommand(order, (await PartAsync(order, alice)).Id));
        var delivered = await Assert.ThrowsAsync<ConflictException>(() =>
            AsSeller(alice, () => SendAsync(new CorrectSaleTrackingCommand(order, "VN-A2"))));
        Assert.Contains("delivered", delivered.Message);
        Assert.Equal("VN-A", (await PartAsync(order, alice)).TrackingReference);
    }

    [Fact]
    public async Task Staff_correct_the_shops_own_part()
    {
        var alice = Guid.CreateVersion7();
        var order = await ShippedAsync([null, alice], null, "SHOP-1");
        _fixture.CurrentUser.Id = Guid.CreateVersion7();

        var detail = await SendAsync(new CorrectShopTrackingCommand(order, "SHOP-2"));

        Assert.Equal("SHOP-2", Assert.Single(detail.Shipments!, s => s.IsShop).TrackingReference);
        Assert.Null((await OrderSeed.ReadAsync(_fixture, order)).TrackingReference);   // two parts: the order has none of its own
        Assert.False(Assert.Single(Notices(order)).Data.ContainsKey("shop"));
    }

    // ------------------------------------------------------------------ helpers

    private List<UserNotificationRequested> Notices(Guid order) =>
        _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Kind == NotificationKind.TrackingCorrected && n.Data.GetValueOrDefault("orderId") == order.ToString()).ToList();

    private List<AuditEntryRecorded> Audited(Guid order) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.Action == "TrackingCorrected" && e.SubjectId == order.ToString()).ToList();

    /// <summary>A paid order with one line per seller (null = the shop), and one of its parts shipped with this reference.</summary>
    private async Task<Guid> ShippedAsync(Guid?[] sellers, Guid? shipper, string tracking)
    {
        var orderId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            context.Orders.Add(new Domain.Entities.Order
            {
                Id = orderId,
                UserId = Guid.CreateVersion7(),
                TotalAmount = 100m * sellers.Length,
                Status = OrderStatus.Paid,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                Currency = "VND",
                Language = "vi",
                ShipTo = new ShippingAddress { RecipientName = "Lan", Line1 = "12 Hang Bac", City = "Hanoi", PostalCode = "100000", Country = "VN" },
                Items = sellers.Select(seller => new OrderItem
                {
                    Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(), ProductName = "Camera",
                    SellerId = seller, SellerName = seller is null ? null : "Mai Lens", Quantity = 1, UnitPrice = 100m,
                }).ToList()
            });
            await context.SaveChangesAsync();
        }

        await using (var scope = _fixture.NewScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            await repository.EnsureShipmentsAsync(orderId);
            await repository.TryMoveShipmentAsync(orderId, shipper, ShipmentStatus.Pending, ShipmentStatus.Preparing, null, DateTime.UtcNow);
            await repository.TryMoveShipmentAsync(orderId, shipper, ShipmentStatus.Preparing, ShipmentStatus.Shipped, tracking, DateTime.UtcNow.AddMinutes(-1));
        }

        return orderId;
    }

    private async Task<OrderShipment> PartAsync(Guid order, Guid? seller)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().OrderShipments.AsNoTracking()
            .SingleAsync(s => s.OrderId == order && s.SellerId == seller);
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
}
