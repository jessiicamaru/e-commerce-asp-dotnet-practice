using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Products.Common;

/// <summary>
/// Picks the price to show, in one place (specs/022).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Nothing here falls back across currencies, and nothing here converts.</b> That is the whole
/// point of the type, and it is deliberately the opposite of <see cref="Localized"/> beside it: a
/// missing translation falls back per field because the worst case is a shopper reading English, and
/// a missing price returns <c>null</c> because the worst case of falling back is selling a
/// 40,000,000₫ camera for 1,600₫.
/// </para>
/// <para>
/// The one fallback that exists is not a fallback between currencies: <c>ProductVariant.Price</c>
/// holds the <b>default currency's</b> amount, so asking for the default currency reads it when no row
/// says otherwise. Asking for any other currency reads rows or nothing.
/// </para>
/// </remarks>
public static class Priced
{
    /// <summary>
    /// What this variant costs in this currency, or <c>null</c> when it is not sold in it.
    /// </summary>
    public static decimal? Of(ProductVariant variant, string currency, string defaultCurrency)
    {
        var row = variant.Prices.FirstOrDefault(p => Same(p.Currency, currency));

        if (row is not null)
        {
            return row.Amount;
        }

        // The default currency lives on the variant itself - one source, not a copy in a second table.
        return Same(currency, defaultCurrency) ? variant.Price : null;
    }

    /// <summary>
    /// The product's "from" price: the cheapest variant that can be sold <b>and</b> has a price in
    /// this currency. <c>null</c> when none has - a product priced only in dong shows no dollar price
    /// rather than a converted one.
    /// </summary>
    public static decimal? FromPriceOf(Product product, string currency, string defaultCurrency) =>
        product.Variants
            .Where(variant => variant.IsActive)
            .Select(variant => Of(variant, currency, defaultCurrency))
            .Where(price => price is not null)
            .DefaultIfEmpty(null)
            .Min();

    /// <summary>
    /// Whether a customer can buy this variant in this currency - both halves of the question. A
    /// variant nobody has priced in dollars is not for sale in dollars, however active it is.
    /// </summary>
    public static bool SellableIn(ProductVariant variant, string currency, string defaultCurrency) =>
        variant.Sellable && Of(variant, currency, defaultCurrency) is not null;

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
