namespace Ecommerce.Shared.Money;

/// <summary>
/// A currency: a code, and how many decimal places amounts in it have (specs/022).
/// </summary>
/// <remarks>
/// <para>
/// The decimals are the part that is easy to forget and impossible to fake. Dong has <b>none</b> -
/// there is no such thing as half a dong - and dollars have two. A system that rounds everything to
/// two produces totals like <c>30.000,50 ₫</c>, which is not a price anybody can pay.
/// </para>
/// <para>
/// <b>There is deliberately no conversion here, and no rate.</b> A price in this shop is entered per
/// currency by an administrator; nothing multiplies one currency by anything to reach another. If a
/// rate ever appears in this type, the feature it belongs to has been misunderstood - see specs/022
/// research D2.
/// </para>
/// </remarks>
/// <param name="Code">ISO 4217, upper case: <c>VND</c>, <c>USD</c>.</param>
/// <param name="Decimals">How many decimal places an amount in it has.</param>
public record Currency(string Code, int Decimals)
{
    /// <summary>
    /// Rounds a computed amount to this currency's minor unit, halves <b>away from zero</b>.
    /// </summary>
    /// <remarks>
    /// Away from zero rather than .NET's default banker's rounding, for the reason
    /// <c>OrderTotals</c> already gives: to-even surprises anyone checking a receipt by hand.
    /// </remarks>
    public decimal Round(decimal amount) => Math.Round(amount, Decimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Whether this is an amount somebody could actually be charged in this currency.
    /// </summary>
    /// <remarks>
    /// <b>9.99 dong is not a price.</b> Rounding what the system COMPUTES is not enough on its own:
    /// an amount entered with more decimals than the currency has flows into a subtotal untouched,
    /// because a subtotal is a unit price times an integer and there is nothing there to round. Found
    /// against the running stack, on an order whose tax came out at a whole 3,003 dong and whose
    /// subtotal was 29.97 (specs/022).
    /// </remarks>
    public bool Fits(decimal amount) => Math.Round(amount, Decimals) == amount;

    public override string ToString() => Code;
}
