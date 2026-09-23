namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>
/// What each part of an order earns whoever ships it (specs/037) - in one pure place, like
/// <see cref="OrderTotals"/>, because it is money and every rounding rule deserves a test of its own.
/// </summary>
public static class Earnings
{
    /// <summary>
    /// The order's delivery charge split equally between <paramref name="parts"/> parts, in the
    /// currency's smallest unit, with whatever does not divide going to the FIRST part (research D2).
    /// The shares always sum to <paramref name="delivery"/> exactly.
    /// </summary>
    /// <param name="decimals">The currency's minor unit: 0 for dong, 2 for dollars (specs/022).</param>
    public static IReadOnlyList<decimal> SplitDelivery(decimal delivery, int parts, int decimals)
    {
        if (parts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parts), parts, "An order has at least one part.");
        }

        // Counted in minor units so the division is whole: 2.00 USD is 200 cents, 200 / 3 is 66 each,
        // and the 2 cents left over go to the first part.
        var unit = Pow10(decimals);
        var minor = decimal.Round(delivery * unit, 0, MidpointRounding.AwayFromZero);
        var each = decimal.Floor(minor / parts);
        var remainder = minor - each * parts;

        return Enumerable.Range(0, parts)
            .Select(i => (i == 0 ? each + remainder : each) / unit)
            .ToList();
    }

    /// <summary>One part's terms, frozen at checkout.</summary>
    /// <param name="goods">Σ unit price × quantity of the part's lines, before tax.</param>
    /// <param name="rate">The marketplace's commission, e.g. 0.10.</param>
    /// <param name="isShop">
    /// The shop's own goods: no commission, because the shop does not charge itself one. The part still
    /// records its delivery share, so an order's shares still add up to its delivery charge.
    /// </param>
    public static PartTerms ForPart(decimal goods, decimal rate, decimal shippingShare, bool isShop, int decimals)
    {
        if (rate < 0 || rate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A commission rate is at least 0 and below 1.");
        }

        // Tax is not in here on purpose (research D3): the shop charged it and the shop remits it.
        var commission = isShop ? 0m : Math.Round(goods * rate, decimals, OrderTotals.Rounding);

        return new PartTerms(goods, commission, shippingShare);
    }

    private static decimal Pow10(int decimals)
    {
        if (decimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "A currency has no negative decimals.");
        }

        var unit = 1m;
        for (var i = 0; i < decimals; i++)
        {
            unit *= 10m;
        }

        return unit;
    }
}

/// <summary>What one part of an order earns: goods, less commission, plus its share of delivery.</summary>
public record PartTerms(decimal GoodsTotal, decimal Commission, decimal ShippingShare)
{
    /// <summary>What the shop owes whoever ships this part.</summary>
    public decimal Payout => GoodsTotal - Commission + ShippingShare;
}
