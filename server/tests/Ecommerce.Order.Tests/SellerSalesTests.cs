using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Application.Orders.Queries.GetMySales;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Catalog;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
// Aliased, not imported: the generated namespace has its own CartItem, which collides with Order's.
using PricedVariant = Ecommerce.Contracts.Grpc.PricedVariant;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A seller can see what they sold (specs/034): the line remembers whose it was, and a seller reads
/// their own lines of paid orders and nothing else.
/// </summary>
/// <remarks>
/// Every test uses sellers of its own. The database is shared by the whole collection, and a seller id
/// minted here is the only way to say "these are all of this seller's sales" without depending on
/// what the other tests left behind.
/// </remarks>
[Collection(nameof(OrderTestCollection))]
public class SellerSalesTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    // ------------------------------------------------------------------ US1: the line remembers

    [Fact]
    public async Task A_sellers_line_records_the_seller()
    {
        var alice = Guid.CreateVersion7();
        var variant = Offer(alice, price: 1000m);

        var order = await CheckoutAsync((variant, 1));

        var line = Assert.Single((await OrderSeed.ReadAsync(_fixture, order)).Items);
        Assert.Equal(alice, line.SellerId);
    }

    /// <summary>The shop's own product has no seller, in the catalogue and so on the line.</summary>
    [Fact]
    public async Task The_shops_line_records_no_seller()
    {
        var variant = Offer(seller: null, price: 1000m);

        var order = await CheckoutAsync((variant, 1));

        Assert.Null(Assert.Single((await OrderSeed.ReadAsync(_fixture, order)).Items).SellerId);
    }

    [Fact]
    public async Task A_mixed_order_records_each_lines_own_seller()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var hers = Offer(alice, price: 1000m);
        var his = Offer(bob, price: 2000m);
        var shops = Offer(seller: null, price: 3000m);

        var order = await CheckoutAsync((hers, 1), (his, 1), (shops, 1));

        var sellers = (await OrderSeed.ReadAsync(_fixture, order)).Items.ToDictionary(i => i.VariantId!.Value, i => i.SellerId);
        Assert.Equal(alice, sellers[hers]);
        Assert.Equal(bob, sellers[his]);
        Assert.Null(sellers[shops]);
    }

    /// <summary>
    /// ⚠️ The presence rule (research D2). A Catalog older than this feature sends no seller at all, and
    /// the shop's own product sends an EMPTY one - both mean "no seller on the line", but only the
    /// first is logged, which is why the wire keeps them apart.
    /// </summary>
    [Fact]
    public void An_absent_or_empty_seller_on_the_wire_is_no_seller_and_a_real_one_is_kept()
    {
        var alice = Guid.CreateVersion7();

        var fromOldCatalog = new PricedVariant { VariantId = Guid.CreateVersion7().ToString() };
        var shops = new PricedVariant { SellerId = string.Empty };
        var hers = new PricedVariant { SellerId = alice.ToString() };

        Assert.False(fromOldCatalog.HasSellerId);
        Assert.Null(GrpcCatalogPrices.SellerOf(fromOldCatalog));
        Assert.True(shops.HasSellerId);
        Assert.Null(GrpcCatalogPrices.SellerOf(shops));
        Assert.Equal(alice, GrpcCatalogPrices.SellerOf(hers));
    }

    // ------------------------------------------------------------------ US2: the list

    [Fact]
    public async Task A_paid_order_with_one_of_their_lines_is_their_sale_counted_over_their_lines_only()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await SeedAsync(OrderStatus.Paid, (alice, 2, 1500m), (bob, 1, 9000m), (null, 1, 400m));

        var sale = Assert.Single((await SalesOfAsync(alice)).Items);

        Assert.Equal(order, sale.OrderId);
        Assert.Equal("Paid", sale.Status);
        Assert.Equal(1, sale.LineCount);
        Assert.Equal(2, sale.Units);
        // 2 × 1,500 - not the order's 12,400, which includes Bob's camera and the shop's strap.
        Assert.Equal(3000m, sale.Subtotal);
        Assert.Equal("VND", sale.Currency);
    }

    [Fact]
    public async Task An_order_holding_only_another_sellers_lines_is_not_their_sale()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        await SeedAsync(OrderStatus.Paid, (bob, 1, 9000m), (null, 1, 400m));

        var page = await SalesOfAsync(alice);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    /// <summary>
    /// A failed order was never a sale, and a submitted one might not become one (research D3). Showing
    /// either would tell a seller they sold something and then take it back.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.Failed)]
    [InlineData(OrderStatus.Submitted)]
    public async Task An_order_that_was_not_paid_is_never_a_sale(OrderStatus status)
    {
        var alice = Guid.CreateVersion7();
        var order = await SeedAsync(status, (alice, 1, 1000m));

        Assert.Empty((await SalesOfAsync(alice)).Items);
        var refused = await Assert.ThrowsAsync<NotFoundException>(() => SaleOfAsync(alice, order));
        Assert.Equal(Sales.NotFound, refused.Message);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, "Paid")]
    [InlineData(OrderStatus.Completed, "Paid")]     // a paid order by its old name (feature 011)
    [InlineData(OrderStatus.Preparing, "Preparing")]
    [InlineData(OrderStatus.Shipped, "Shipped")]
    public async Task A_paid_order_stays_a_sale_as_it_moves_on(OrderStatus status, string reported)
    {
        var alice = Guid.CreateVersion7();
        await SeedAsync(status, (alice, 1, 1000m));

        Assert.Equal(reported, Assert.Single((await SalesOfAsync(alice)).Items).Status);
    }

    [Fact]
    public async Task Sales_come_newest_first_a_page_at_a_time_with_the_sellers_own_total()
    {
        var alice = Guid.CreateVersion7();
        var oldest = await SeedAsync(OrderStatus.Paid, [(alice, 1, 1m)], DateTime.UtcNow.AddDays(-3));
        var middle = await SeedAsync(OrderStatus.Shipped, [(alice, 1, 1m)], DateTime.UtcNow.AddDays(-2));
        var newest = await SeedAsync(OrderStatus.Paid, [(alice, 1, 1m)], DateTime.UtcNow.AddDays(-1));

        var first = await SalesOfAsync(alice, page: 1, pageSize: 2);
        var second = await SalesOfAsync(alice, page: 2, pageSize: 2);

        Assert.Equal([newest, middle], first.Items.Select(s => s.OrderId));
        Assert.Equal([oldest], second.Items.Select(s => s.OrderId));
        Assert.Equal(3, first.TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task A_page_out_of_range_is_refused(int page, int pageSize)
    {
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => SalesOfAsync(Guid.CreateVersion7(), page, pageSize));
    }

    // ------------------------------------------------------------------ US3: one sale

    [Fact]
    public async Task One_sale_shows_only_the_sellers_own_lines()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var order = await SeedAsync(OrderStatus.Paid, (alice, 2, 1500m), (bob, 1, 9000m), (null, 1, 400m));

        var sale = await SaleOfAsync(alice, order);

        var line = Assert.Single(sale.Items);
        Assert.Equal("Alice's camera", line.ProductName);
        Assert.Equal(3000m, line.TotalPrice);
        Assert.Equal(3000m, sale.Subtotal);
        Assert.Equal("vi", sale.Language);
    }

    /// <summary>
    /// ⚠️ The specs/027 rule, applied to sales (research D5). An order that exists but is not their sale
    /// must be refused in the very words used for one that does not exist, or the refusal confirms
    /// that somebody else sold something on it.
    /// </summary>
    [Fact]
    public async Task Not_theirs_and_not_there_are_refused_in_the_same_words()
    {
        var alice = Guid.CreateVersion7();
        var bobs = await SeedAsync(OrderStatus.Paid, (Guid.CreateVersion7(), 1, 9000m));
        var shops = await SeedAsync(OrderStatus.Paid, (null, 1, 400m));

        var notTheirs = await Assert.ThrowsAsync<NotFoundException>(() => SaleOfAsync(alice, bobs));
        var theShops = await Assert.ThrowsAsync<NotFoundException>(() => SaleOfAsync(alice, shops));
        var notThere = await Assert.ThrowsAsync<NotFoundException>(() => SaleOfAsync(alice, Guid.CreateVersion7()));

        Assert.Equal(notThere.Message, notTheirs.Message);
        Assert.Equal(notThere.Message, theShops.Message);
    }

    /// <summary>
    /// What a seller does NOT get (research D4), asserted on the shape rather than on one response - a
    /// field added to the record later would be empty in every existing test and pass all of them.
    /// </summary>
    [Theory]
    [InlineData(typeof(SaleDetailResponse))]
    [InlineData(typeof(SaleSummaryResponse))]
    public void A_sale_carries_nothing_about_the_customer_or_the_rest_of_the_order(Type shape)
    {
        string[] forbidden =
        [
            "UserId", "CustomerId", "Email", "ShippingAddress", "ShipTo", "TotalAmount",
            "ShippingPrice", "ShippingOption", "TaxTotal", "DiscountTotal", "TrackingReference"
        ];

        var present = shape.GetProperties().Select(p => p.Name).Intersect(forbidden).ToList();

        Assert.True(present.Count == 0, $"{shape.Name} discloses: {string.Join(", ", present)}");
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A variant Catalog will price, belonging to <paramref name="seller"/>.</summary>
    private Guid Offer(Guid? seller, decimal price)
    {
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        _fixture.Checkout.Prices[variant] = new CatalogPrice(
            product, "Camera", price, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller);
        return variant;
    }

    private async Task<Guid> CheckoutAsync(params (Guid Variant, int Quantity)[] lines)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = lines
            .Select(l => new CartItem(_fixture.Checkout.Prices[l.Variant].ProductId, l.Quantity, l.Variant))
            .ToList();
        _fixture.Checkout.Address = Home;

        await using var scope = _fixture.NewScope();
        var response = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SubmitOrderCommand(null, "standard"));
        return response.OrderId;
    }

    private Task<Guid> SeedAsync(OrderStatus status, params (Guid? Seller, int Quantity, decimal Price)[] lines) =>
        SeedAsync(status, lines, DateTime.UtcNow.AddMinutes(-5));

    /// <summary>
    /// An order already in <paramref name="status"/>, written straight to the table. The read side is
    /// what is under test here; checkout is covered above.
    /// </summary>
    private async Task<Guid> SeedAsync(
        OrderStatus status, (Guid? Seller, int Quantity, decimal Price)[] lines, DateTime createdAt)
    {
        var orderId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var items = lines.Select(l => new OrderItem
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            ProductId = Guid.CreateVersion7(),
            ProductName = l.Seller is null ? "The shop's strap" : "Alice's camera",
            SellerId = l.Seller,
            Quantity = l.Quantity,
            UnitPrice = l.Price
        }).ToList();

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId,
            UserId = Guid.CreateVersion7(),
            TotalAmount = items.Sum(i => i.TotalPrice),
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Currency = "VND",
            Language = "vi",
            Items = items
        });

        await context.SaveChangesAsync();
        return orderId;
    }

    private async Task<PagedResponse<SaleSummaryResponse>> SalesOfAsync(Guid seller, int page = 1, int pageSize = 20)
    {
        _fixture.CurrentUser.Id = seller;
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMySalesQuery(page, pageSize));
    }

    private async Task<SaleDetailResponse> SaleOfAsync(Guid seller, Guid orderId)
    {
        _fixture.CurrentUser.Id = seller;
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMySaleQuery(orderId));
    }
}
