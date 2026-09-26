using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Insights;
using Ecommerce.Order.Application.Orders.Commands.CancelOrder;
using Ecommerce.Order.Application.Orders.Commands.FailOrder;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Returns;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Vouchers at checkout, against the real PostgreSQL (specs/069): the quote and the order agree, the lines freeze
/// what was taken off, the uses are claimed once however many race for the last one, a failed or cancelled order
/// gives them back once, and the money downstream - a seller's terms, a refund, a seller's revenue - follows.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class VoucherCheckoutTests
{
    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private readonly OrderTestFixture _fixture;

    public VoucherCheckoutTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    [Fact]
    public async Task The_quote_and_the_order_agree_and_the_order_freezes_what_was_taken_off()
    {
        var code = await PlatformVoucherAsync("Percent", percent: 30m, maxDiscount: 200_000m, minSubtotal: 1_000_000m);
        var customer = Customer();
        Cart((null, 600_000m, 2));   // 1,200,000: 30% is 360,000, capped at 200,000

        var quote = await SendAsync(new GetCheckoutQuoteQuery(null, "standard", [code]));
        var placed = await SendAsync(new SubmitOrderCommand(null, "standard", [code]));

        Assert.Equal(200_000m, quote.DiscountTotal);
        Assert.Equal(quote.TotalAmount, placed.TotalAmount);
        Assert.Equal(quote.TaxTotal, placed.TaxTotal);
        // Tax on what is paid: 10% of 1,000,000 goods and of 30,000 delivery.
        Assert.Equal(100_000m + 3_000m, placed.TaxTotal);
        Assert.Equal(1_200_000m + 30_000m + 103_000m - 200_000m, placed.TotalAmount);
        Assert.Equal(code, Assert.Single(placed.Vouchers!).Code);

        var order = await ReadAsync(placed.OrderId);
        Assert.Equal(200_000m, order.Items.Single().PlatformDiscount);
        Assert.Equal(order.TotalAmount, order.Subtotal + order.ShippingPrice + order.TaxTotal - order.DiscountTotal);   // the CHECK's identity
        Assert.Equal((code, 200_000m, customer), order.Vouchers.Select(v => (v.Code, v.Amount, v.CustomerId)).Single());
        Assert.Equal(1, await UsedAsync(code));

        var detail = await SendAsync(new GetMyOrderByIdQuery(placed.OrderId));
        Assert.Equal(200_000m, detail.Items.Single().Discount);
        Assert.Equal(code, Assert.Single(detail.Vouchers!).Code);
    }

    [Fact]
    public async Task Free_delivery_takes_the_delivery_and_its_tax_off()
    {
        var code = await PlatformVoucherAsync("FreeShipping");
        Customer();
        Cart((null, 100_000m, 1));

        var placed = await SendAsync(new SubmitOrderCommand(null, "standard", [code]));

        Assert.Equal(30_000m, placed.DiscountTotal);
        Assert.Equal(10_000m, placed.TaxTotal);   // the goods' only
        Assert.Equal(110_000m, placed.TotalAmount);
    }

    /// <summary>Research D1: the seller pays for their own voucher, and the shop for its own.</summary>
    [Fact]
    public async Task A_shop_voucher_comes_out_of_the_sellers_terms_and_a_platform_voucher_does_not()
    {
        var mai = Guid.CreateVersion7();
        var shopCode = await ShopVoucherAsync(mai, "FixedAmount", fixedValue: 100_000m);
        var platformCode = await PlatformVoucherAsync("FixedAmount", fixedValue: 50_000m);
        Customer();
        Cart((mai, 1_000_000m, 1));

        var placed = await SendAsync(new SubmitOrderCommand(null, "standard", [shopCode, platformCode]));

        var order = await ReadAsync(placed.OrderId);
        var line = order.Items.Single();
        Assert.Equal((100_000m, 50_000m), (line.ShopDiscount, line.PlatformDiscount));
        var part = order.Shipments.Single(s => s.SellerId == mai);
        Assert.Equal(900_000m, part.GoodsTotal);        // less her own voucher, not the platform's
        Assert.Equal(90_000m, part.Commission);         // 10% of what she sold it for
        Assert.Equal(150_000m, placed.DiscountTotal);
    }

    [Fact]
    public async Task A_refused_voucher_places_nothing()
    {
        var code = await PlatformVoucherAsync("Percent", percent: 10m, minSubtotal: 5_000_000m);
        var customer = Customer();
        Cart((null, 100_000m, 1));

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new SubmitOrderCommand(null, "standard", [code])));

        Assert.Contains("at least 5000000 VND", refused.Message);
        Assert.Equal(0, await UsedAsync(code));
        Assert.False(await OrdersOfAsync(customer));
    }

    /// <summary>Research D5: ten customers race for the last use - one order, nine refusals, the counter at its limit.</summary>
    [Fact]
    public async Task Of_many_checkouts_racing_for_the_last_use_exactly_one_is_placed()
    {
        var code = await PlatformVoucherAsync("FixedAmount", fixedValue: 1_000m, totalLimit: 1);
        Customer();
        Cart((null, 100_000m, 1));

        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                return (await SendAsync(new SubmitOrderCommand(null, "standard", [code]))).OrderId;
            }
            catch (ConflictException)
            {
                return Guid.Empty;
            }
        }));

        Assert.Single(attempts, id => id != Guid.Empty);
        Assert.Equal(1, await UsedAsync(code));
        Assert.Equal(1, await RedemptionsAsync(code));
    }

    [Fact]
    public async Task One_customer_racing_themselves_past_their_limit_gets_one_order()
    {
        var code = await PlatformVoucherAsync("FixedAmount", fixedValue: 1_000m, perCustomerLimit: 1);
        Customer();
        Cart((null, 100_000m, 1));

        var placed = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await SendAsync(new SubmitOrderCommand(null, "standard", [code]));
                return 1;
            }
            catch (ConflictException)
            {
                return 0;
            }
        }));

        Assert.Equal(1, placed.Sum());
        var again = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new SubmitOrderCommand(null, "standard", [code])));
        Assert.Contains("already used", again.Message);
    }

    /// <summary>A failed checkout gives its use back once: two orders hold it, one fails twice, one use remains.</summary>
    [Fact]
    public async Task A_failed_order_gives_its_use_back_once()
    {
        var code = await PlatformVoucherAsync("FixedAmount", fixedValue: 1_000m, perCustomerLimit: 5);
        var customer = Customer();
        Cart((null, 100_000m, 1));
        var failing = (await SendAsync(new SubmitOrderCommand(null, "standard", [code]))).OrderId;
        await SendAsync(new SubmitOrderCommand(null, "standard", [code]));
        Assert.Equal(2, await UsedAsync(code));

        await SendAsync(new FailOrderCommand(failing, "Payment declined", DateTime.UtcNow));
        await SendAsync(new FailOrderCommand(failing, "Payment declined", DateTime.UtcNow));

        Assert.Equal(1, await UsedAsync(code));
        Assert.Equal(1, await CustomerUsesAsync(code, customer));
        Assert.NotNull((await ReadAsync(failing)).Vouchers.Single().ReleasedAt);
    }

    [Fact]
    public async Task A_cancelled_order_gives_its_use_back_and_the_customer_may_use_it_again()
    {
        var code = await PlatformVoucherAsync("FixedAmount", fixedValue: 1_000m, perCustomerLimit: 1, totalLimit: 1);
        var customer = Customer();
        Cart((null, 100_000m, 1));
        var order = (await SendAsync(new SubmitOrderCommand(null, "standard", [code]))).OrderId;
        await SettleAsync(order);

        _fixture.CurrentUser.Id = customer;
        await SendAsync(new CancelMyOrderCommand(order));

        Assert.Equal(0, await UsedAsync(code));
        Assert.Equal(0, await CustomerUsesAsync(code, customer));
        Cart((null, 100_000m, 1));
        Assert.Single((await SendAsync(new SubmitOrderCommand(null, "standard", [code]))).Vouchers!);
    }

    [Fact]
    public async Task A_new_customer_voucher_is_refused_after_a_sold_order()
    {
        var code = await PlatformVoucherAsync("FixedAmount", fixedValue: 50_000m, conditions: [new VoucherConditionRequest("NewCustomer", null)]);
        Customer();
        Cart((null, 100_000m, 1));
        Assert.Single((await SendAsync(new GetCheckoutQuoteQuery(null, "standard", [code]))).Vouchers!);   // first time: fine

        var first = (await SendAsync(new SubmitOrderCommand(null, "standard", []))).OrderId;
        await SettleAsync(first);

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new GetCheckoutQuoteQuery(null, "standard", [code])));
        Assert.Contains("first order", refused.Message);
    }

    /// <summary>A returned parcel refunds what was paid for it: the price less the discount, plus the tax on that.</summary>
    [Fact]
    public void A_returned_parcel_refunds_what_was_paid_after_the_discount()
    {
        var parcel = new ReturnParcel(Guid.Empty, Guid.Empty, Guid.Empty, null, OrderStatus.Shipped, ShipmentStatus.Shipped, DateTime.UtcNow, "VND",
            [new ReturnParcelLine(Guid.Empty, 2, 500_000m, 80_000m, 200_000m)]);

        Assert.Equal(1_000_000m - 200_000m + 80_000m, parcel.RefundAmount);
    }

    [Fact]
    public async Task A_sellers_revenue_is_less_their_own_voucher_and_not_the_platforms()
    {
        var mai = Guid.CreateVersion7();
        var shopCode = await ShopVoucherAsync(mai, "FixedAmount", fixedValue: 100_000m);
        var platformCode = await PlatformVoucherAsync("FixedAmount", fixedValue: 50_000m);
        Customer();
        Cart((mai, 1_000_000m, 1));
        var order = (await SendAsync(new SubmitOrderCommand(null, "standard", [shopCode, platformCode]))).OrderId;
        await SettleAsync(order);
        var day = new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(0, 3000));
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Where(o => o.Id == order)
                .ExecuteUpdateAsync(x => x.SetProperty(o => o.CreatedAt, day.AddHours(12)));
        }

        _fixture.CurrentUser.Id = mai;
        var revenue = await SendAsync(new GetSellerRevenueQuery(day, day));

        Assert.Equal(900_000m, Assert.Single(revenue.Totals).Revenue);
    }

    // ------------------------------------------------------------------ helpers

    private Guid Customer()
    {
        var customer = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = customer;
        _fixture.CurrentUser.Roles.Clear();
        return customer;
    }

    private void Cart(params (Guid? Seller, decimal Price, int Quantity)[] lines)
    {
        var cart = new List<CartItem>();
        foreach (var (seller, price, quantity) in lines)
        {
            var product = Guid.CreateVersion7();
            var variant = Guid.CreateVersion7();
            _fixture.Checkout.Prices[variant] = new CatalogPrice(
                product, "Camera", price, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", seller, seller is null ? null : "Mai's cameras");
            cart.Add(new CartItem(product, quantity, variant));
        }

        _fixture.Checkout.Cart = cart;
        _fixture.Checkout.Address = Home;
    }

    private Task<string> PlatformVoucherAsync(
        string benefit, decimal? percent = null, decimal? fixedValue = null, decimal? maxDiscount = null, decimal? minSubtotal = null,
        int? totalLimit = null, int? perCustomerLimit = null, List<VoucherConditionRequest>? conditions = null) =>
        CreateAsync(Guid.CreateVersion7(), "Admin", benefit, percent, fixedValue, maxDiscount, minSubtotal, totalLimit, perCustomerLimit, conditions);

    private Task<string> ShopVoucherAsync(Guid seller, string benefit, decimal? percent = null, decimal? fixedValue = null) =>
        CreateAsync(seller, "Seller", benefit, percent, fixedValue, null, null, null, null, null);

    private async Task<string> CreateAsync(
        Guid creator, string role, string benefit, decimal? percent, decimal? fixedValue, decimal? maxDiscount, decimal? minSubtotal,
        int? totalLimit, int? perCustomerLimit, List<VoucherConditionRequest>? conditions)
    {
        var who = (_fixture.CurrentUser.Id, _fixture.CurrentUser.Roles.ToList());
        _fixture.CurrentUser.Id = creator;
        _fixture.CurrentUser.Roles.Clear();
        _fixture.CurrentUser.Roles.Add(role);
        try
        {
            var code = $"T{Guid.NewGuid():N}"[..16].ToUpperInvariant();
            await SendAsync(new CreateVoucherCommand(code, "Test voucher", benefit, percent, DateTime.UtcNow.AddMinutes(-1), null, totalLimit, perCustomerLimit,
                [new VoucherAmountRequest("VND", fixedValue, maxDiscount, minSubtotal)], conditions, null));
            return code;
        }
        finally
        {
            _fixture.CurrentUser.Id = who.Item1;
            _fixture.CurrentUser.Roles.Clear();
            foreach (var r in who.Item2) _fixture.CurrentUser.Roles.Add(r);
        }
    }

    private async Task SettleAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
            .TrySettleAsync(order, OrderStatus.Paid, null, DateTime.UtcNow));
    }

    private async Task<Domain.Entities.Order> ReadAsync(Guid order)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.Shipments).Include(o => o.Vouchers)
            .SingleAsync(o => o.Id == order);
    }

    private async Task<int> UsedAsync(string code)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Vouchers.Where(v => v.Code == code).Select(v => v.UsedCount).SingleAsync();
    }

    private async Task<int> RedemptionsAsync(string code)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().VoucherRedemptions.CountAsync(r => r.Code == code);
    }

    private async Task<int> CustomerUsesAsync(string code, Guid customer)
    {
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var id = await db.Vouchers.Where(v => v.Code == code).Select(v => v.Id).SingleAsync();
        return await db.VoucherCustomerUses.Where(u => u.VoucherId == id && u.CustomerId == customer).Select(u => u.Uses).SingleOrDefaultAsync();
    }

    private async Task<bool> OrdersOfAsync(Guid customer)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.AnyAsync(o => o.UserId == customer);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
