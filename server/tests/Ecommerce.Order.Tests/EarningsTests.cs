using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Infrastructure.Marketplace;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Order.Tests;

/// <summary>
/// How an order's delivery charge is split between its parts and what each seller earns (specs/037).
/// Pure arithmetic, no database - but it is money, so every rounding rule gets a case.
/// </summary>
public class EarningsTests
{
    [Theory]
    [InlineData(30000, 1, 0, new[] { "30000" })]
    [InlineData(30000, 2, 0, new[] { "15000", "15000" })]
    [InlineData(30000, 3, 0, new[] { "10000", "10000", "10000" })]
    // Dong has no minor unit: 30,001 over 2 is 15,001 + 15,000, never 15,000.5 twice.
    [InlineData(30001, 2, 0, new[] { "15001", "15000" })]
    [InlineData(60000, 7, 0, new[] { "8574", "8571", "8571", "8571", "8571", "8571", "8571" })]
    // Cents: $2.00 over 3 is 0.68 / 0.66 / 0.66 - the remainder goes to the first part.
    [InlineData(2, 3, 2, new[] { "0.68", "0.66", "0.66" })]
    [InlineData(0, 3, 0, new[] { "0", "0", "0" })]
    public void The_delivery_charge_is_split_equally_and_sums_exactly(
        decimal delivery, int parts, int decimals, string[] expected)
    {
        var shares = Earnings.SplitDelivery(delivery, parts, decimals);

        Assert.Equal(expected.Select(decimal.Parse), shares);
        Assert.Equal(delivery, shares.Sum());
    }

    [Fact]
    public void An_order_with_no_parts_has_nothing_to_split()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Earnings.SplitDelivery(30000m, 0, 0));
    }

    [Fact]
    public void A_seller_earns_their_goods_less_commission_plus_their_delivery_share()
    {
        var terms = Earnings.ForPart(goods: 1_200_000m, rate: 0.10m, shippingShare: 15_000m, isShop: false, decimals: 0);

        Assert.Equal(1_200_000m, terms.GoodsTotal);
        Assert.Equal(120_000m, terms.Commission);
        Assert.Equal(15_000m, terms.ShippingShare);
        Assert.Equal(1_095_000m, terms.Payout);
    }

    /// <summary>ADR-002's rounding - half away from zero - not .NET's banker's rounding to even.</summary>
    [Theory]
    [InlineData(25, 0.10, 0, 3)]         // 2.5 → 3, where banker's rounding gives 2
    [InlineData(15, 0.10, 0, 2)]         // 1.5 → 2
    [InlineData(0.25, 0.10, 2, 0.03)]    // 0.025 → 0.03, where banker's rounding gives 0.02
    public void Commission_is_rounded_half_away_from_zero_to_the_currency(
        decimal goods, decimal rate, int decimals, decimal expected)
    {
        Assert.Equal(expected, Earnings.ForPart(goods, rate, 0m, isShop: false, decimals).Commission);
    }

    /// <summary>The shop does not charge itself commission; its part keeps its delivery share.</summary>
    [Fact]
    public void The_shop_s_own_part_takes_no_commission()
    {
        var terms = Earnings.ForPart(500_000m, 0.10m, 10_000m, isShop: true, decimals: 0);

        Assert.Equal(0m, terms.Commission);
        Assert.Equal(10_000m, terms.ShippingShare);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1)]
    public void A_rate_outside_0_to_1_is_refused(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Earnings.ForPart(100m, rate, 0m, false, 0));
    }

    // ------------------------------------------------------------------ the configured rate (FR-008)

    [Fact]
    public void The_configured_rate_is_read()
    {
        Assert.Equal(0.1m, new ConfiguredCommissionRate(Config("0.1")).Current);
    }

    /// <summary>
    /// ⚠️ Missing is not zero. Reading it as zero would start cleanly and give every sale away without a
    /// word; refusing to start is the loud version of the same mistake.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("ten percent")]
    [InlineData("1")]
    [InlineData("-0.05")]
    public void Order_does_not_start_without_a_sensible_rate(string? raw)
    {
        Assert.Throws<InvalidOperationException>(() => new ConfiguredCommissionRate(Config(raw)));
    }

    private static IConfiguration Config(string? rate) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Marketplace:CommissionRate"] = rate })
            .Build();
}
