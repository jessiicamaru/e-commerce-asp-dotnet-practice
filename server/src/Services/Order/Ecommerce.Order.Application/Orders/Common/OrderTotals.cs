namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>
/// How an order's total is reached - the whole of it, in one pure function (feature 012).
/// </summary>
/// <remarks>
/// <para>
/// <b>Prices exclude tax</b> (ADR-002). Tax is added per line and on delivery, each rounded to the
/// currency's minor unit with halves rounded <b>away from zero</b>, and the rounded amounts are
/// summed. .NET's default is banker's rounding (to even), which surprises anyone checking a receipt
/// by hand.
/// </para>
/// <para>
/// Per-line rounding can differ from rounding the sum by up to half a cent per line. That is accepted
/// and not "corrected": the per-line tax stored is what was charged (specs/012 research D3).
/// </para>
/// </remarks>
public static class OrderTotals
{
    public const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    /// <summary>
    /// How many decimal places an amount has when nobody says - two, which is what every caller
    /// assumed before specs/022 and what a dollar actually has.
    /// </summary>
    public const int DefaultDecimals = 2;

    public static decimal TaxOn(decimal net, decimal rate, int decimals = DefaultDecimals) =>
        Math.Round(net * rate, decimals, Rounding);

    /// <param name="decimals">
    /// The currency's minor unit (specs/022): <b>0</b> for dong, 2 for dollars. Rounding every
    /// currency to two produces totals like <c>30.000,50 ₫</c>, which is not a price anybody can pay.
    /// The parts are summed <i>after</i> rounding and the total is computed from them, so the CHECK
    /// constraint on the order's parts holds exactly whatever this is.
    /// </param>
    /// <param name="lineDiscounts">
    /// What vouchers took off each line (specs/069), in the order of <paramref name="lines"/>; null for none.
    /// Tax is on what is left - the customer pays tax on what they pay (research D2).
    /// </param>
    /// <param name="deliveryDiscount">What a free-delivery voucher took off the delivery; its tax goes with it.</param>
    public static Result Compute(
        IReadOnlyList<(decimal UnitPrice, int Quantity)> lines,
        decimal delivery,
        decimal rate,
        int decimals = DefaultDecimals,
        IReadOnlyList<decimal>? lineDiscounts = null,
        decimal deliveryDiscount = 0m)
    {
        if (rate < 0 || rate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A tax rate is at least 0 and below 1.");
        }

        if (decimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "A currency has no negative decimals.");
        }

        if (lineDiscounts is not null && lineDiscounts.Count != lines.Count)
        {
            throw new ArgumentException("One discount per line.", nameof(lineDiscounts));
        }

        var discounts = lineDiscounts ?? Enumerable.Repeat(0m, lines.Count).ToList();
        if (discounts.Where((d, i) => d < 0 || d > lines[i].UnitPrice * lines[i].Quantity).Any()
            || deliveryDiscount < 0 || deliveryDiscount > delivery)
        {
            throw new ArgumentOutOfRangeException(nameof(lineDiscounts), "A discount is never negative nor more than what it is off.");
        }

        var lineTaxes = lines.Select((l, i) => TaxOn(l.UnitPrice * l.Quantity - discounts[i], rate, decimals)).ToList();
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var deliveryTax = TaxOn(delivery - deliveryDiscount, rate, decimals);
        var tax = lineTaxes.Sum() + deliveryTax;

        // The discount part (specs/012) is every voucher's amount: the goods' and the delivery's. Subtotal and
        // delivery stay the prices before it, so the order's parts still add up to its total - the CHECK holds.
        var discount = discounts.Sum() + deliveryDiscount;

        return new Result(subtotal, delivery, lineTaxes, deliveryTax, tax, discount, subtotal + delivery + tax - discount);
    }

    public record Result(
        decimal Subtotal,
        decimal Delivery,
        IReadOnlyList<decimal> LineTaxes,
        decimal DeliveryTax,
        decimal Tax,
        decimal Discount,
        decimal Total);
}
