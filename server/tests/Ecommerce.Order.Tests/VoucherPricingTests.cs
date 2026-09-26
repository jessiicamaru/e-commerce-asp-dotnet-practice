using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What vouchers take off (specs/069) - pure, like <see cref="OrderTotalsTests"/>: every rule, every refusal and
/// every rounding, with no database. The checkout's own tests (<see cref="VoucherCheckoutTests"/>) prove it is
/// the code that charges.
/// </summary>
public class VoucherPricingTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Mai = Guid.CreateVersion7();
    private static readonly Guid Bao = Guid.CreateVersion7();

    // ---------------------------------------------------------------------------------- the user's examples

    [Fact]
    public void Below_the_minimum_spend_is_refused_with_the_minimum_in_words()
    {
        var sale = Platform("SALE30", VoucherBenefit.Percent, 30m, amount: new VoucherAmount { Currency = "VND", MinSubtotal = 1_000_000m, MaxDiscount = 200_000m });

        var refused = Assert.Throws<ConflictException>(() => Apply([Line(999_999m, 1)], sale));

        Assert.Contains("SALE30", refused.Message);
        Assert.Contains("1000000 VND", refused.Message);
    }

    [Fact]
    public void Over_the_minimum_takes_thirty_percent_until_the_cap()
    {
        var sale = Platform("SALE30", VoucherBenefit.Percent, 30m, amount: new VoucherAmount { Currency = "VND", MinSubtotal = 1_000_000m, MaxDiscount = 200_000m });

        var capped = Apply([Line(500_000m, 2)], sale);   // 30% of 1,000,000 is 300,000: capped at 200,000
        Assert.Equal(200_000m, capped.PlatformDiscounts.Sum());
        Assert.Equal(200_000m, capped.Applied.Single().Amount);
        Assert.Equal(180_000m, Apply([Line(600_000m, 1)], NoMinimum(sale)).PlatformDiscounts.Sum());   // under the cap
    }

    [Fact]
    public void Twenty_percent_off_one_variant_touches_that_line_only()
    {
        var lens = Line(1_000_000m, 1);
        var body = Line(30_000_000m, 1);
        var v = Platform("LENS20", VoucherBenefit.Percent, 20m, targets: [new VoucherTarget { Type = VoucherTargetType.Variant, TargetId = lens.VariantId }]);

        var r = Apply([body, lens], v);

        Assert.Equal([0m, 200_000m], r.PlatformDiscounts);
    }

    [Fact]
    public void A_product_target_takes_every_variant_of_it()
    {
        var product = Guid.CreateVersion7();
        var black = Line(1_000m, 1) with { ProductId = product };
        var silver = Line(3_000m, 1) with { ProductId = product };
        var v = Platform("FUJI10", VoucherBenefit.Percent, 10m, targets: [new VoucherTarget { Type = VoucherTargetType.Product, TargetId = product }]);

        Assert.Equal([100m, 300m, 0m], Apply([black, silver, Line(5_000m, 1)], v).PlatformDiscounts);
    }

    [Fact]
    public void A_new_customer_voucher_is_refused_to_somebody_who_has_bought()
    {
        var welcome = Platform("WELCOME", VoucherBenefit.FixedAmount, amount: new VoucherAmount { Currency = "VND", FixedValue = 50_000m },
            conditions: [new VoucherCondition { Type = VoucherConditionType.NewCustomer }]);

        Assert.Equal(50_000m, Apply([Line(300_000m, 1)], welcome).PlatformDiscounts.Sum());
        var refused = Assert.Throws<ConflictException>(() =>
            Apply([Line(300_000m, 1)], [welcome], facts: new CustomerFacts(true, new HashSet<Guid>())));
        Assert.Contains("first order", refused.Message);
    }

    [Fact]
    public void Free_delivery_takes_the_delivery_off_up_to_its_cap()
    {
        var ship = Platform("FREESHIP", VoucherBenefit.FreeShipping, amount: new VoucherAmount { Currency = "VND", MaxDiscount = 30_000m });

        Assert.Equal(30_000m, Apply([Line(100_000m, 1)], [ship], delivery: 45_000m).DeliveryDiscount);
        Assert.Equal(20_000m, Apply([Line(100_000m, 1)], [ship], delivery: 20_000m).DeliveryDiscount);
        Assert.Throws<ConflictException>(() => Apply([Line(100_000m, 1)], [ship], delivery: 0m));
    }

    // ---------------------------------------------------------------------------------- shops and the platform

    [Fact]
    public void A_shop_voucher_touches_only_that_shops_lines_and_is_the_sellers_discount()
    {
        var hers = Line(1_000_000m, 1, Mai);
        var his = Line(1_000_000m, 1, Bao);
        var v = Shop("MAI10", Mai, VoucherBenefit.Percent, 10m);

        var r = Apply([hers, his], v);

        Assert.Equal([100_000m, 0m], r.ShopDiscounts);
        Assert.Equal([0m, 0m], r.PlatformDiscounts);
    }

    [Fact]
    public void A_shop_voucher_with_none_of_its_goods_in_the_cart_applies_to_nothing()
    {
        var refused = Assert.Throws<ConflictException>(() => Apply([Line(1_000m, 1, Bao)], Shop("MAI10", Mai, VoucherBenefit.Percent, 10m)));
        Assert.Contains("does not apply to anything", refused.Message);
    }

    /// <summary>The shop's cut first, then the platform's on what is left: 10% then 10% is 19%, not 20%.</summary>
    [Fact]
    public void Shop_vouchers_come_first_and_the_platform_applies_to_what_is_left()
    {
        var r = Apply([Line(1_000_000m, 1, Mai)], Shop("MAI10", Mai, VoucherBenefit.Percent, 10m), Platform("ALL10", VoucherBenefit.Percent, 10m));

        Assert.Equal([100_000m], r.ShopDiscounts);
        Assert.Equal([90_000m], r.PlatformDiscounts);
        Assert.Equal(["MAI10", "ALL10"], r.Applied.Select(a => a.Code));
    }

    [Fact]
    public void One_voucher_per_shop_one_platform_voucher_and_one_free_delivery()
    {
        var lines = new[] { Line(1_000_000m, 1, Mai), Line(1_000_000m, 1, Bao) };

        // Each shop's, the platform's and free delivery together: fine.
        var r = Apply(lines, [Shop("MAI5", Mai, VoucherBenefit.Percent, 5m), Shop("BAO5", Bao, VoucherBenefit.Percent, 5m),
            Platform("ALL5", VoucherBenefit.Percent, 5m), Platform("SHIP", VoucherBenefit.FreeShipping, amount: new VoucherAmount { Currency = "VND" })],
            delivery: 30_000m);
        Assert.Equal(4, r.Applied.Count);

        Assert.Contains("one voucher per shop", Assert.Throws<ConflictException>(() =>
            Apply(lines, Shop("MAI5", Mai, VoucherBenefit.Percent, 5m), Shop("MAI6", Mai, VoucherBenefit.Percent, 6m))).Message);
        Assert.Contains("one shop-wide voucher", Assert.Throws<ConflictException>(() =>
            Apply(lines, Platform("A5", VoucherBenefit.Percent, 5m), Platform("B5", VoucherBenefit.Percent, 5m))).Message);
    }

    // ---------------------------------------------------------------------------------- money

    [Fact]
    public void A_voucher_with_no_amount_in_the_orders_currency_is_refused_even_a_percentage()
    {
        var dongOnly = Platform("SALE10", VoucherBenefit.Percent, 10m);

        var refused = Assert.Throws<ConflictException>(() =>
            VoucherPricing.Apply([Line(100m, 1)], 0m, "USD", 2, ["SALE10"], [new VoucherInput(dongOnly, 0)], CustomerFacts.None, Now));

        Assert.Contains("cannot be used in USD", refused.Message);
    }

    [Fact]
    public void A_fixed_amount_is_never_more_than_what_it_applies_to()
    {
        var v = Platform("BIG", VoucherBenefit.FixedAmount, amount: new VoucherAmount { Currency = "VND", FixedValue = 500_000m });

        Assert.Equal([100_000m], Apply([Line(100_000m, 1)], v).PlatformDiscounts);
    }

    /// <summary>100,000 over three equal lines: 33,333 each and the one left over to the first of the largest.</summary>
    [Fact]
    public void A_discount_is_spread_in_proportion_with_the_remainder_to_the_largest_and_adds_up_exactly()
    {
        var v = Platform("FIX", VoucherBenefit.FixedAmount, amount: new VoucherAmount { Currency = "VND", FixedValue = 100_000m });

        var even = Apply([Line(300_000m, 1), Line(300_000m, 1), Line(300_000m, 1)], v).PlatformDiscounts;
        Assert.Equal([33_334m, 33_333m, 33_333m], even);

        var uneven = Apply([Line(100_000m, 1), Line(300_000m, 1)], v).PlatformDiscounts;
        Assert.Equal([25_000m, 75_000m], uneven);
    }

    [Fact]
    public void Dollars_round_to_cents_half_away_from_zero()
    {
        var v = Platform("CENTS", VoucherBenefit.Percent, 12.5m, amount: new VoucherAmount { Currency = "USD" });

        // 12.5% of 0.20 = 0.025 -> 0.03, not banker's 0.02
        var r = VoucherPricing.Apply([Line(0.20m, 1)], 0m, "USD", 2, ["CENTS"], [new VoucherInput(v, 0)], CustomerFacts.None, Now);

        Assert.Equal([0.03m], r.PlatformDiscounts);
    }

    // ---------------------------------------------------------------------------------- when a code cannot be used

    [Fact]
    public void Unknown_and_disabled_codes_read_the_same()
    {
        var disabled = Platform("OFF", VoucherBenefit.Percent, 10m);
        disabled.Status = VoucherStatus.Disabled;

        Assert.Equal("Voucher NOPE cannot be used.", Assert.Throws<ConflictException>(() => Apply([Line(1m, 1)], ["NOPE"], [])).Message);
        Assert.Equal("Voucher OFF cannot be used.", Assert.Throws<ConflictException>(() => Apply([Line(1m, 1)], disabled)).Message);
    }

    [Fact]
    public void Dates_and_limits_are_refused_in_words()
    {
        var later = Platform("LATER", VoucherBenefit.Percent, 10m);
        later.StartsAt = Now.AddDays(1);
        var over = Platform("OVER", VoucherBenefit.Percent, 10m);
        over.EndsAt = Now;
        var gone = Platform("GONE", VoucherBenefit.Percent, 10m);
        (gone.TotalLimit, gone.UsedCount) = (100, 100);
        var once = Platform("ONCE", VoucherBenefit.Percent, 10m);
        once.PerCustomerLimit = 1;

        Assert.Contains("starts on", Assert.Throws<ConflictException>(() => Apply([Line(1m, 1)], later)).Message);
        Assert.Contains("expired", Assert.Throws<ConflictException>(() => Apply([Line(1m, 1)], over)).Message);
        Assert.Contains("used up", Assert.Throws<ConflictException>(() => Apply([Line(1m, 1)], gone)).Message);
        Assert.Contains("already used", Assert.Throws<ConflictException>(() =>
            VoucherPricing.Apply([Line(1m, 1)], 0m, "VND", 0, ["ONCE"], [new VoucherInput(once, 1)], CustomerFacts.None, Now)).Message);
    }

    [Fact]
    public void Minimum_quantity_and_first_order_in_the_shop()
    {
        var two = Shop("TWO", Mai, VoucherBenefit.Percent, 10m, conditions: [new VoucherCondition { Type = VoucherConditionType.MinQuantity, Value = 2 }]);
        var first = Shop("FIRST", Mai, VoucherBenefit.Percent, 10m, conditions: [new VoucherCondition { Type = VoucherConditionType.FirstOrderInShop }]);

        Assert.Contains("at least 2 items", Assert.Throws<ConflictException>(() => Apply([Line(1_000m, 1, Mai), Line(1_000m, 5, Bao)], two)).Message);
        Assert.Equal(200m, Apply([Line(1_000m, 2, Mai)], two).ShopDiscounts.Sum());
        Assert.Throws<ConflictException>(() => Apply([Line(1_000m, 1, Mai)], [first], facts: new CustomerFacts(true, new HashSet<Guid> { Mai })));
        Assert.Equal(100m, Apply([Line(1_000m, 1, Mai)], [first], facts: new CustomerFacts(true, new HashSet<Guid> { Bao })).ShopDiscounts.Sum());
    }

    [Fact]
    public void Codes_are_matched_whatever_their_case_and_more_than_five_are_refused()
    {
        var v = Platform("SALE10", VoucherBenefit.Percent, 10m);

        Assert.Equal(100m, Apply([Line(1_000m, 1)], [" sale10 "], [v]).PlatformDiscounts.Sum());
        Assert.Contains("At most 5", Assert.Throws<ConflictException>(() => Apply([Line(1_000m, 1)], ["A", "B", "C", "D", "E", "F"], [])).Message);
    }

    [Fact]
    public void No_codes_take_nothing_off()
    {
        var r = Apply([Line(1_000m, 2)], [], []);

        Assert.Equal([0m], r.ShopDiscounts);
        Assert.Equal([0m], r.PlatformDiscounts);
        Assert.Equal(0m, r.DeliveryDiscount);
        Assert.Empty(r.Applied);
    }

    // ---------------------------------------------------------------------------------- the total

    /// <summary>Research D2: tax is on what is paid, and the parts still add up to the total.</summary>
    [Fact]
    public void Tax_is_on_the_discounted_price_and_the_parts_still_sum_to_the_total()
    {
        var r = OrderTotals.Compute([(1_000_000m, 1), (500_000m, 2)], delivery: 30_000m, rate: 0.10m, decimals: 0,
            lineDiscounts: [200_000m, 0m], deliveryDiscount: 30_000m);

        Assert.Equal([80_000m, 100_000m], r.LineTaxes);
        Assert.Equal(0m, r.DeliveryTax);
        Assert.Equal(230_000m, r.Discount);
        Assert.Equal(2_000_000m + 30_000m + 180_000m - 230_000m, r.Total);
        Assert.Equal(r.Total, r.Subtotal + r.Delivery + r.Tax - r.Discount);
    }

    [Fact]
    public void A_discount_larger_than_its_line_is_a_programming_error_not_a_refund()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderTotals.Compute([(100m, 1)], delivery: 0m, rate: 0m, lineDiscounts: [101m]));
    }

    // ---------------------------------------------------------------------------------- helpers

    private static VoucherLine Line(decimal price, int quantity, Guid? seller = null) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), seller, price, quantity);

    private static VoucherResult Apply(VoucherLine[] lines, params Voucher[] vouchers) =>
        Apply(lines, vouchers.Select(v => v.Code).ToArray(), vouchers);

    private static VoucherResult Apply(VoucherLine[] lines, Voucher[] vouchers, decimal delivery = 0m, CustomerFacts? facts = null) =>
        VoucherPricing.Apply(lines, delivery, "VND", 0, vouchers.Select(v => v.Code).ToList(),
            vouchers.Select(v => new VoucherInput(v, 0)).ToList(), facts ?? CustomerFacts.None, Now);

    private static VoucherResult Apply(VoucherLine[] lines, string[] codes, Voucher[] vouchers) =>
        VoucherPricing.Apply(lines, 0m, "VND", 0, codes, vouchers.Select(v => new VoucherInput(v, 0)).ToList(), CustomerFacts.None, Now);

    private static Voucher NoMinimum(Voucher v)
    {
        v.Amounts[0].MinSubtotal = null;
        return v;
    }

    private static Voucher Platform(string code, VoucherBenefit benefit, decimal? percent = null, VoucherAmount? amount = null,
        List<VoucherTarget>? targets = null, List<VoucherCondition>? conditions = null) =>
        New(code, null, benefit, percent, amount, targets, conditions);

    private static Voucher Shop(string code, Guid seller, VoucherBenefit benefit, decimal? percent = null, VoucherAmount? amount = null,
        List<VoucherTarget>? targets = null, List<VoucherCondition>? conditions = null) =>
        New(code, seller, benefit, percent, amount, targets, conditions);

    private static Voucher New(string code, Guid? seller, VoucherBenefit benefit, decimal? percent, VoucherAmount? amount,
        List<VoucherTarget>? targets, List<VoucherCondition>? conditions) => new()
    {
        Id = Guid.CreateVersion7(),
        Code = code,
        Name = code,
        SellerId = seller,
        Benefit = benefit,
        Percent = percent,
        StartsAt = Now.AddDays(-1),
        Status = VoucherStatus.Active,
        Amounts = [amount ?? new VoucherAmount { Currency = "VND" }],
        Targets = targets ?? [],
        Conditions = conditions ?? [],
    };
}
