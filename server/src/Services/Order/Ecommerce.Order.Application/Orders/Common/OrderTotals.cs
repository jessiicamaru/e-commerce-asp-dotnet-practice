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
    public static Result Compute(
        IReadOnlyList<(decimal UnitPrice, int Quantity)> lines,
        decimal delivery,
        decimal rate,
        int decimals = DefaultDecimals)
    {
        if (rate < 0 || rate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A tax rate is at least 0 and below 1.");
        }

        if (decimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "A currency has no negative decimals.");
        }

        var lineTaxes = lines.Select(l => TaxOn(l.UnitPrice * l.Quantity, rate, decimals)).ToList();
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var deliveryTax = TaxOn(delivery, rate, decimals);
        var tax = lineTaxes.Sum() + deliveryTax;
        const decimal discount = 0m;   // discounts are out of scope (FR-007); the part exists for later

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
