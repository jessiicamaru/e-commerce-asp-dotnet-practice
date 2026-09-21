using Ecommerce.Order.Application.Orders.Common;

namespace Ecommerce.Order.Tests;

/// <summary>
/// The arithmetic of a total (feature 012, ADR-002): tax per line and on delivery, rounded half away
/// from zero, then summed. Pure - no database, because the rule is arithmetic, not storage.
/// </summary>
public class OrderTotalsTests
{
    [Fact]
    public void Half_a_cent_rounds_away_from_zero_not_to_even()
    {
        // 0.25 x 10% = 0.025. Banker's rounding (the .NET default) gives 0.02; a receipt reader expects 0.03.
        Assert.Equal(0.03m, OrderTotals.TaxOn(0.25m, 0.10m));

        // 0.45 x 10% = 0.045 -> to-even would give 0.04.
        Assert.Equal(0.05m, OrderTotals.TaxOn(0.45m, 0.10m));
    }

    [Fact]
    public void Tax_is_rounded_per_line_and_on_delivery_then_summed()
    {
        // Three lines of 0.25 at 10%: per line 0.025 -> 0.03 each = 0.09. Rounding the sum instead
        // (0.075 -> 0.08) would give a different answer; the per-line one is the rule (research D3).
        var r = OrderTotals.Compute([(0.25m, 1), (0.25m, 1), (0.25m, 1)], delivery: 0.25m, rate: 0.10m);

        Assert.Equal([0.03m, 0.03m, 0.03m], r.LineTaxes);
        Assert.Equal(0.03m, r.DeliveryTax);
        Assert.Equal(0.12m, r.Tax);
        Assert.Equal(0.75m, r.Subtotal);
        Assert.Equal(1.12m, r.Total);
    }

    [Fact]
    public void The_parts_always_sum_to_the_total()
    {
        var r = OrderTotals.Compute([(9.99m, 3), (1499.99m, 2)], delivery: 15m, rate: 0.19m);

        Assert.Equal(r.Total, r.Subtotal + r.Delivery + r.Tax - r.Discount);
        Assert.Equal(0m, r.Discount);
    }

    [Fact]
    public void A_zero_rate_is_zero_tax_not_a_missing_figure()
    {
        var r = OrderTotals.Compute([(9.99m, 3)], delivery: 5m, rate: 0m);

        Assert.Equal(0m, r.Tax);
        Assert.Equal(34.97m, r.Total);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.00)]
    public void An_impossible_rate_is_refused(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderTotals.Compute([(1m, 1)], 0m, (decimal)rate));
    }
}
