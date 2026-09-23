using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.Fulfilment;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrders;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Each seller ships their own part of an order, and the shop ships its own (specs/035).
/// </summary>
/// <remarks>
/// Every test mints its own sellers: the database is shared by the collection, and "this seller's
/// parts" means nothing unless nobody else can have made one.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class ShipmentTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    // ------------------------------------------------------------------ parts exist

    [Fact]
    public async Task Checkout_writes_one_part_per_seller_and_one_for_the_shop()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await CheckoutAsync((alice, 2), (alice, 1), (bob, 1), (null, 1));

        var parts = await PartsAsync(order);

        Assert.Equal(3, parts.Count);
        // Compared as a SET. Two v7 Guids minted in the same millisecond have no reliable order - the
        // first version sorted them and failed half the time on code nobody had touched.
        Assert.Equal(new HashSet<Guid?> { null, alice, bob }, parts.Select(p => p.SellerId).ToHashSet());
        Assert.All(parts, p => Assert.Equal(ShipmentStatus.Pending, p.Status));
    }

    // ------------------------------------------------------------------ a seller moves their part

    [Fact]
    public async Task A_seller_prepares_and_ships_their_part_and_nobody_else_s()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, bob, null);

        var prepared = await AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        Assert.Equal("Preparing", prepared.Status);

        var shipped = await AsSeller(alice, () => SendAsync(new ShipMySaleCommand(order, "VNPOST-A")));
        Assert.Equal("Shipped", shipped.Status);
        Assert.Equal("VNPOST-A", shipped.TrackingReference);

        var parts = await PartsAsync(order);
        Assert.Equal(ShipmentStatus.Shipped, parts.Single(p => p.SellerId == alice).Status);
        Assert.Equal(ShipmentStatus.Pending, parts.Single(p => p.SellerId == bob).Status);
        Assert.Equal(ShipmentStatus.Pending, parts.Single(p => p.SellerId == null).Status);

        // The order says "somebody has started": Preparing, with no single tracking reference to give.
        var row = await OrderSeed.ReadAsync(_fixture, order);
        Assert.Equal(OrderStatus.Preparing, row.Status);
        Assert.Null(row.TrackingReference);
    }

    /// <summary>
    /// ⚠️ The specs/034 rule, now on a write. Another seller's part, the shop's, a missing order - one
    /// wording, or the refusal tells a seller the order exists and somebody else sold on it.
    /// </summary>
    [Fact]
    public async Task A_part_that_is_not_theirs_is_not_found_in_the_same_words_as_no_order()
    {
        var alice = Guid.CreateVersion7();
        var carol = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, null);

        var notTheirs = await Assert.ThrowsAsync<NotFoundException>(
            () => AsSeller(carol, () => SendAsync(new PrepareMySaleCommand(order))));
        var notThere = await Assert.ThrowsAsync<NotFoundException>(
            () => AsSeller(carol, () => SendAsync(new PrepareMySaleCommand(Guid.CreateVersion7()))));

        Assert.Equal(notThere.Message, notTheirs.Message);
        Assert.All(await PartsAsync(order), p => Assert.Equal(ShipmentStatus.Pending, p.Status));
    }

    [Theory]
    [InlineData(OrderStatus.Submitted)]
    [InlineData(OrderStatus.Failed)]
    public async Task A_part_of_an_unpaid_order_cannot_be_started(OrderStatus status)
    {
        var alice = Guid.CreateVersion7();
        var order = await SeedAsync(status, [alice]);

        var refused = await Assert.ThrowsAsync<NotFoundException>(
            () => AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order))));

        Assert.Equal(Sales.NotFound, refused.Message);
    }

    /// <summary>A double click, a retried request: the second changes nothing and is not an error.</summary>
    [Fact]
    public async Task Repeating_a_step_changes_nothing()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, Guid.CreateVersion7());
        await AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        var before = await OrderSeed.ReadAsync(_fixture, order);
        var partBefore = (await PartsAsync(order)).Single(p => p.SellerId == alice);

        var again = await AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order)));

        Assert.Equal("Preparing", again.Status);
        Assert.Equal(before.UpdatedAt, (await OrderSeed.ReadAsync(_fixture, order)).UpdatedAt);
        Assert.Equal(partBefore.UpdatedAt, (await PartsAsync(order)).Single(p => p.SellerId == alice).UpdatedAt);
    }

    [Fact]
    public async Task Shipping_before_preparing_and_preparing_after_shipping_are_refused()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice);

        await Assert.ThrowsAsync<ConflictException>(
            () => AsSeller(alice, () => SendAsync(new ShipMySaleCommand(order, "EARLY"))));

        await AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order)));
        await AsSeller(alice, () => SendAsync(new ShipMySaleCommand(order, "VN1")));

        await Assert.ThrowsAsync<ConflictException>(
            () => AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order))));
        // Shipped again with a DIFFERENT reference is not a repeat - it would rewrite what the customer follows.
        await Assert.ThrowsAsync<ConflictException>(
            () => AsSeller(alice, () => SendAsync(new ShipMySaleCommand(order, "VN2"))));
        Assert.Equal("VN1", (await PartsAsync(order)).Single().TrackingReference);
    }

    [Fact]
    public async Task Shipping_needs_a_tracking_reference()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice);
        await AsSeller(alice, () => SendAsync(new PrepareMySaleCommand(order)));

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => AsSeller(alice, () => SendAsync(new ShipMySaleCommand(order, "  "))));
    }

    // ------------------------------------------------------------------ the order's summary

    [Fact]
    public async Task The_order_is_shipped_when_every_part_is_and_not_before()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, bob, null);

        await ShipAsSellerAsync(alice, order, "A");
        await ShipAsSellerAsync(bob, order, "B");
        Assert.Equal(OrderStatus.Preparing, (await OrderSeed.ReadAsync(_fixture, order)).Status);

        await SendAsync(new PrepareOrderCommand(order));
        await SendAsync(new ShipOrderCommand(order, "SHOP"));

        var row = await OrderSeed.ReadAsync(_fixture, order);
        Assert.Equal(OrderStatus.Shipped, row.Status);
        Assert.Null(row.TrackingReference); // three parcels: no one of them is "the order's"
    }

    /// <summary>
    /// ⚠️ research D5. Two sellers shipping the last two parts at the same moment must leave the order
    /// Shipped. Without the order's row lock each computes the summary from a snapshot in which the
    /// other's part is not shipped yet, and the order stays Preparing for good. Many orders at once, so
    /// the window is actually hit rather than hoped for.
    /// </summary>
    [Fact]
    public async Task Two_parts_shipped_at_the_same_moment_leave_the_order_shipped()
    {
        var pairs = new List<(Guid Order, Guid A, Guid B)>();
        for (var i = 0; i < 15; i++)
        {
            var a = Guid.CreateVersion7();
            var b = Guid.CreateVersion7();
            var order = await PaidOrderAsync(a, b);
            await MoveAsync(order, a, ShipmentStatus.Pending, ShipmentStatus.Preparing, null);
            await MoveAsync(order, b, ShipmentStatus.Pending, ShipmentStatus.Preparing, null);
            pairs.Add((order, a, b));
        }

        await Task.WhenAll(pairs.SelectMany(p => new[]
        {
            MoveAsync(p.Order, p.A, ShipmentStatus.Preparing, ShipmentStatus.Shipped, "A"),
            MoveAsync(p.Order, p.B, ShipmentStatus.Preparing, ShipmentStatus.Shipped, "B"),
        }));

        foreach (var (order, _, _) in pairs)
        {
            Assert.Equal(OrderStatus.Shipped, (await OrderSeed.ReadAsync(_fixture, order)).Status);
        }
    }

    // ------------------------------------------------------------------ the shop's part

    /// <summary>The staff endpoints move the shop's part - and leave every seller's where it was.</summary>
    [Fact]
    public async Task Staff_move_the_shop_s_part_and_nothing_a_seller_sold()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, null);

        await SendAsync(new PrepareOrderCommand(order));

        var parts = await PartsAsync(order);
        Assert.Equal(ShipmentStatus.Preparing, parts.Single(p => p.SellerId == null).Status);
        Assert.Equal(ShipmentStatus.Pending, parts.Single(p => p.SellerId == alice).Status);
    }

    [Fact]
    public async Task An_order_with_nothing_of_the_shop_s_has_nothing_for_staff_to_ship()
    {
        var order = await PaidOrderAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new PrepareOrderCommand(order)));

        Assert.Contains("each seller ships their own", refused.Message);
    }

    /// <summary>One parcel: the order reads exactly as it did before parts existed, tracking and all.</summary>
    [Fact]
    public async Task An_order_in_one_parcel_carries_that_parcel_s_tracking_reference()
    {
        var order = await PaidOrderAsync((Guid?)null);

        await SendAsync(new PrepareOrderCommand(order));
        var shipped = await SendAsync(new ShipOrderCommand(order, "VN-ONE"));

        Assert.Equal("Shipped", shipped.Status);
        Assert.Equal("VN-ONE", shipped.TrackingReference);
    }

    // ------------------------------------------------------------------ orders from before

    /// <summary>
    /// research D3: an order written by an image that predates parts has none. The first step on it
    /// creates them in the state the ORDER is in - so an order an older image had moved to Preparing is a
    /// part that can be shipped, not one waiting to be prepared a second time.
    /// </summary>
    [Fact]
    public async Task Parts_missing_from_an_older_order_are_made_in_the_order_s_state()
    {
        var alice = Guid.CreateVersion7();
        var order = await SeedAsync(OrderStatus.Preparing, [alice, null]);
        Assert.Empty(await PartsAsync(order));

        var shipped = await ShipAsSellerAsync(alice, order, "LEGACY", prepare: false);

        Assert.Equal("Shipped", shipped.Status);
        var parts = await PartsAsync(order);
        Assert.Equal(2, parts.Count);
        Assert.Equal(ShipmentStatus.Preparing, parts.Single(p => p.SellerId == null).Status);
    }

    // ------------------------------------------------------------------ what each side reads

    /// <summary>research D6: the address is the job, and leaves when the job is done.</summary>
    [Fact]
    public async Task A_seller_sees_the_address_until_their_part_is_shipped_and_not_after()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, Guid.CreateVersion7());

        var waiting = await AsSeller(alice, () => SendAsync(new GetMySaleQuery(order)));
        Assert.Equal("Paid", waiting.Status);
        Assert.Equal("Ha Noi", waiting.ShippingAddress?.City);
        Assert.Equal("+84 912 345 678", waiting.ShippingAddress?.Phone);

        var shipped = await ShipAsSellerAsync(alice, order, "VN-A");
        Assert.Null(shipped.ShippingAddress);
        Assert.Equal("VN-A", shipped.TrackingReference);
    }

    /// <summary>A seller's sale reads THEIR part: another seller's parcel going out says nothing about theirs.</summary>
    [Fact]
    public async Task A_sale_reads_the_seller_s_own_part_not_the_order()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, bob);

        await ShipAsSellerAsync(alice, order, "A");

        var bobs = await AsSeller(bob, () => SendAsync(new GetMySaleQuery(order)));
        Assert.Equal("Paid", bobs.Status);
        Assert.Null(bobs.TrackingReference);
    }

    [Fact]
    public async Task The_customer_sees_every_parcel_and_how_many_have_gone()
    {
        var alice = Guid.CreateVersion7();
        var order = await PaidOrderAsync(alice, null);
        await ShipAsSellerAsync(alice, order, "VN-A");

        _fixture.CurrentUser.Id = (await OrderSeed.ReadAsync(_fixture, order)).UserId;
        var detail = await SendAsync(new GetMyOrderByIdQuery(order));

        Assert.Equal("Preparing", detail.Status);
        Assert.NotNull(detail.Shipments);
        Assert.Equal(2, detail.Shipments!.Count);
        Assert.Equal(["Paid", "Shipped"], detail.Shipments.Select(s => s.Status));
        Assert.Equal("VN-A", detail.Shipments[1].TrackingReference);
        Assert.Equal(["Alice's camera"], detail.Shipments[1].Items);

        var row = Assert.Single((await SendAsync(new GetMyOrdersQuery())).Items, o => o.OrderId == order);
        Assert.Equal(2, row.ShipmentCount);
        Assert.Equal(1, row.ShipmentsShipped);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<SaleDetailResponse> ShipAsSellerAsync(Guid seller, Guid order, string tracking, bool prepare = true)
    {
        if (prepare)
        {
            await AsSeller(seller, () => SendAsync(new PrepareMySaleCommand(order)));
        }

        return await AsSeller(seller, () => SendAsync(new ShipMySaleCommand(order, tracking)));
    }

    private async Task MoveAsync(Guid order, Guid? seller, ShipmentStatus from, ShipmentStatus to, string? tracking)
    {
        await using var scope = _fixture.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
            .TryMoveShipmentAsync(order, seller, from, to, tracking, DateTime.UtcNow);
        Assert.Equal(ShipmentMoveOutcome.Moved, result.Outcome);
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

    /// <summary>A paid order with one line per given seller (null = the shop), and its parts in place.</summary>
    private async Task<Guid> PaidOrderAsync(params Guid?[] sellers)
    {
        var order = await SeedAsync(OrderStatus.Paid, sellers);
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<IOrderRepository>().EnsureShipmentsAsync(order);
        return order;
    }

    /// <summary>An order written straight to the table, WITHOUT parts - as an older image would write it.</summary>
    private async Task<Guid> SeedAsync(OrderStatus status, Guid?[] sellers)
    {
        var orderId = Guid.CreateVersion7();
        var at = DateTime.UtcNow.AddMinutes(-5);

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId,
            UserId = Guid.CreateVersion7(),
            TotalAmount = 100m * sellers.Length,
            Status = status,
            CreatedAt = at,
            UpdatedAt = at,
            Currency = "VND",
            Language = "vi",
            ShipTo = new ShippingAddress
            {
                RecipientName = Home.RecipientName, Line1 = Home.Line1, City = Home.City,
                PostalCode = Home.PostalCode, Country = Home.Country, Phone = Home.Phone
            },
            Items = sellers.Select(seller => new OrderItem
            {
                Id = Guid.CreateVersion7(),
                OrderId = orderId,
                ProductId = Guid.CreateVersion7(),
                ProductName = seller is null ? "The shop's strap" : "Alice's camera",
                SellerId = seller,
                Quantity = 1,
                UnitPrice = 100m
            }).ToList()
        });

        await context.SaveChangesAsync();
        return orderId;
    }

    private async Task<Guid> CheckoutAsync(params (Guid? Seller, int Quantity)[] lines)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        var cart = new List<CartItem>();
        foreach (var (seller, quantity) in lines)
        {
            var product = Guid.CreateVersion7();
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, "Camera", 1000m, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller);
            cart.Add(new CartItem(product, quantity, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
        return (await SendAsync(new SubmitOrderCommand(null, "standard"))).OrderId;
    }

    private async Task<List<OrderShipment>> PartsAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>()
            .OrderShipments.AsNoTracking().Where(s => s.OrderId == order).ToListAsync();
    }
}
