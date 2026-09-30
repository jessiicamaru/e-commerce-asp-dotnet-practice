using Ecommerce.Order.Application.MyData;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What keeps an account from being deleted (specs/112, #217): each blocker for the person whose business it is,
/// from the caller's token - and never somebody else's business.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class AccountStandingTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task Nothing_open_blocks_nothing()
    {
        var buyer = Guid.CreateVersion7();
        await OrderAsync(buyer, OrderStatus.Shipped, delivered: true);
        await OrderAsync(buyer, OrderStatus.Failed, delivered: false);
        await OrderAsync(buyer, OrderStatus.Cancelled, delivered: false);

        Assert.Empty(await StandingOfAsync(buyer));
    }

    [Theory]
    [InlineData(OrderStatus.Submitted)]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Shipped)]
    public async Task An_order_settling_or_on_its_way_is_open(OrderStatus status)
    {
        var buyer = Guid.CreateVersion7();
        await OrderAsync(buyer, status, delivered: false);

        Assert.Equal([AccountBlockers.OpenOrders], await StandingOfAsync(buyer));
    }

    [Fact]
    public async Task A_paid_order_without_parts_is_open_and_a_cancelled_part_is_not()
    {
        var buyer = Guid.CreateVersion7();
        await OrderAsync(buyer, OrderStatus.Paid, delivered: false, withPart: false);
        Assert.Equal([AccountBlockers.OpenOrders], await StandingOfAsync(buyer));

        var other = Guid.CreateVersion7();
        await OrderAsync(other, OrderStatus.Shipped, delivered: false, cancelled: true);
        Assert.Empty(await StandingOfAsync(other));
    }

    [Theory]
    [InlineData(ReturnStatus.Requested, true)]
    [InlineData(ReturnStatus.Accepted, true)]
    [InlineData(ReturnStatus.Escalated, true)]
    [InlineData(ReturnStatus.SentBack, true)]
    [InlineData(ReturnStatus.Refused, false)]
    [InlineData(ReturnStatus.Rejected, false)]
    [InlineData(ReturnStatus.Received, false)]
    public async Task A_return_waiting_for_somebody_is_open_for_the_buyer_and_the_seller(ReturnStatus status, bool open)
    {
        var buyer = Guid.CreateVersion7();
        var seller = Guid.CreateVersion7();
        var (orderId, partId) = await OrderAsync(buyer, OrderStatus.Shipped, delivered: true, seller: seller, payout: true);
        await ReturnAsync(orderId, partId, buyer, seller, status);

        var expected = open ? new[] { AccountBlockers.OpenReturns } : [];
        Assert.Equal(expected, await StandingOfAsync(buyer));
        Assert.Equal(expected, await StandingOfAsync(seller));
    }

    [Fact]
    public async Task A_sellers_part_on_its_way_is_an_open_sale_and_money_not_paid_out_is_unpaid()
    {
        var seller = Guid.CreateVersion7();
        await OrderAsync(Guid.CreateVersion7(), OrderStatus.Preparing, delivered: false, seller: seller);

        Assert.Equal([AccountBlockers.OpenSales, AccountBlockers.UnpaidEarnings], await StandingOfAsync(seller));
    }

    [Fact]
    public async Task A_delivered_part_is_unpaid_until_a_payout_claims_it()
    {
        var unpaid = Guid.CreateVersion7();
        await OrderAsync(Guid.CreateVersion7(), OrderStatus.Shipped, delivered: true, seller: unpaid);
        Assert.Equal([AccountBlockers.UnpaidEarnings], await StandingOfAsync(unpaid));

        var paid = Guid.CreateVersion7();
        await OrderAsync(Guid.CreateVersion7(), OrderStatus.Shipped, delivered: true, seller: paid, payout: true);
        Assert.Empty(await StandingOfAsync(paid));
    }

    [Fact]
    public async Task Somebody_elses_business_blocks_nobody_else()
    {
        var buyer = Guid.CreateVersion7();
        var seller = Guid.CreateVersion7();
        var (orderId, partId) = await OrderAsync(buyer, OrderStatus.Shipped, delivered: false, seller: seller);
        await ReturnAsync(orderId, partId, buyer, seller, ReturnStatus.Requested);

        Assert.Empty(await StandingOfAsync(Guid.CreateVersion7()));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(Guid Order, Guid Part)> OrderAsync(
        Guid buyer, OrderStatus status, bool delivered, Guid? seller = null, bool withPart = true, bool cancelled = false, bool payout = false)
    {
        var orderId = Guid.CreateVersion7();
        var partId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        Guid? payoutId = null;
        if (payout && seller is not null)
        {
            payoutId = Guid.CreateVersion7();
            db.Payouts.Add(new Payout
            {
                Id = payoutId.Value, SellerId = seller.Value, Currency = "VND", Amount = 9m, PartCount = 1,
                RecordedBy = Guid.CreateVersion7(), CreatedAt = now,
            });
        }

        db.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId, UserId = buyer, TotalAmount = 10m, Status = status, Currency = "VND", CreatedAt = now, UpdatedAt = now,
            Items = [new OrderItem { Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(), ProductName = "Lens", SellerId = seller, Quantity = 1, UnitPrice = 10m }],
            Shipments = withPart
                ?
                [
                    new OrderShipment
                    {
                        Id = partId, OrderId = orderId, SellerId = seller, Status = delivered ? ShipmentStatus.Shipped : ShipmentStatus.Pending,
                        UpdatedAt = now, ShippedAt = delivered ? now : null, DeliveredAt = delivered ? now : null,
                        CancelledAt = cancelled ? now : null, GoodsTotal = seller is null ? null : 10m, Commission = seller is null ? null : 1m,
                        ShippingShare = seller is null ? null : 0m, PayoutId = payoutId,
                    },
                ]
                : [],
        });
        await db.SaveChangesAsync();
        return (orderId, partId);
    }

    private async Task ReturnAsync(Guid orderId, Guid partId, Guid buyer, Guid seller, ReturnStatus status)
    {
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.ParcelReturns.Add(new ParcelReturn
        {
            Id = Guid.CreateVersion7(), OrderId = orderId, ShipmentId = partId, CustomerId = buyer, SellerId = seller, Status = status,
            Reason = "Wrong lens", RequestedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> StandingOfAsync(Guid caller)
    {
        _fixture.CurrentUser.Id = caller;
        _fixture.CurrentUser.Roles.Clear();
        _fixture.CurrentUser.Roles.Add("Customer");
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMyStandingQuery());
    }
}
