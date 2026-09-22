namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// The ways an order can be sent, and what each costs. Owned by Order, because Order prices checkout.
/// </summary>
public interface IShippingOptions
{
    IReadOnlyList<ShippingOption> All { get; }

    /// <summary>Case-insensitive; <c>null</c> when there is no such option.</summary>
    ShippingOption? Find(string? code);

    /// <summary>
    /// The options that can actually be chosen in this currency - the ones somebody has priced in it
    /// (specs/022 FR-008). Offering a delivery whose price is unknown would put an amount in one
    /// currency next to amounts in another, which is the defect this feature exists to end.
    /// </summary>
    IReadOnlyList<ShippingOption> Offered(string currency);
}

/// <param name="Prices">
/// What it costs, per currency. A currency absent from here is one this delivery is <b>not offered
/// in</b> - never one to convert into (specs/022).
/// </param>
public record ShippingOption(string Code, string Name, IReadOnlyDictionary<string, decimal> Prices)
{
    /// <summary>What it costs in this currency, or <c>null</c> when it is not offered in it.</summary>
    public decimal? PriceIn(string currency) =>
        Prices.TryGetValue(currency.ToUpperInvariant(), out var price) ? price : null;
}
