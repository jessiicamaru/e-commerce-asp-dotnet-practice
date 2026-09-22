namespace Ecommerce.Catalog.Application.Products.Common;

/// <summary>
/// What Inventory last said about a product's buyability, as it goes over the wire.
/// </summary>
/// <remarks>
/// A string rather than a boolean, matching how order status is already sent. It leaves room for a
/// third value later — a low-stock state — without changing the field's type, and
/// <c>"availability": "InStock"</c> reads correctly on its own where <c>true</c> would have to be
/// mentally attached to the field name.
/// </remarks>
public static class ProductAvailability
{
    public const string InStock = "InStock";

    /// <summary>
    /// Also what a product Inventory has never announced reads as. Showing "out of stock" for
    /// something in stock costs a sale and self-corrects; showing "in stock" for something gone
    /// takes an order that cannot be filled.
    /// </summary>
    public const string OutOfStock = "OutOfStock";

    public static string From(bool isAvailable) => isAvailable ? InStock : OutOfStock;
}

/// <summary>
/// A product as a shopper sees it.
/// </summary>
/// <remarks>
/// <b>There is deliberately no stock count here.</b> This service does not own stock and cannot
/// stand behind a number. Issue #4 was a <c>StockQuantity</c> on this record, assigned once at
/// creation and never written again, shown to anonymous shoppers deciding whether to buy. A real
/// figure comes from <c>GET /api/stock/{productId}</c> on Inventory, which is already public.
/// <para>
/// <c>ImageUrl</c> is null when there is no image, and changes whenever the image does (specs/019).
/// </para>
/// <para>
/// <c>Price</c> is the CHEAPEST active variant's price since specs/020, and <c>PriceVaries</c> says
/// whether to show it as a "from" price. <c>Variants</c> is filled on the product lookup and null on
/// the listing, which would otherwise carry every shape of every product on the page.
/// It is additive, so older clients ignore it.
/// </para>
/// <para>
/// Since specs/022 <c>Price</c> is <b>nullable</b> and <c>Currency</c> says what it is in: a product
/// no variant of which is priced in the currency being asked about has no price in it, and saying so
/// is the point. Nothing is converted to fill the gap.
/// </para>
/// </remarks>
public record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal? Price,
    string Availability,
    string Sku,
    Guid CategoryId,
    bool IsActive,
    string? ImageUrl = null,
    bool PriceVaries = false,
    int VariantCount = 1,
    List<VariantResponse>? Variants = null,
    string Language = "",
    string Currency = "",
    Guid? SellerId = null,
    string? SellerName = null
)
{
    /// <summary>
    /// The one place a product becomes a response, so the image address is never forgotten by one of
    /// the three handlers that build it.
    /// </summary>
    public static ProductResponse From(
        Domain.Entities.Product p,
        string language = "",
        string defaultLanguage = "",
        string currency = "",
        string defaultCurrency = "",
        string? sellerName = null)
        => Build(p, withVariants: false, language, defaultLanguage, currency, defaultCurrency, sellerName);

    /// <summary>The product with every shape it is sold in - what the product page needs (specs/020).</summary>
    public static ProductResponse WithVariants(
        Domain.Entities.Product p,
        string language = "",
        string defaultLanguage = "",
        string currency = "",
        string defaultCurrency = "",
        string? sellerName = null)
        => Build(p, withVariants: true, language, defaultLanguage, currency, defaultCurrency, sellerName);

    private static ProductResponse Build(
        Domain.Entities.Product p,
        bool withVariants,
        string language,
        string defaultLanguage,
        string currency,
        string defaultCurrency,
        string? sellerName = null)
    {
        // An empty language means "whatever is stored" - the shape every caller had before specs/021,
        // and what a consumer with no request uses. An empty currency means the same for the price.
        var localise = !string.IsNullOrEmpty(language);
        var price = !string.IsNullOrEmpty(currency);
        var active = p.Variants.Where(v => v.IsActive).ToList();

        // Only the variants actually priced in this currency have a say in the "from" price and in
        // whether it varies - a variant nobody priced in dollars is not a cheap dollar option.
        var prices = price
            ? active.Select(v => Priced.Of(v, currency, defaultCurrency)).Where(a => a is not null).ToList()
            : active.Select(v => (decimal?)v.Price).ToList();

        return new(
            p.Id,
            localise ? Localized.NameOf(p, language) : p.Name,
            localise ? Localized.DescriptionOf(p, language) : p.Description,
            // The "from" price: the cheapest active variant priced in this currency. Falls back to the
            // product's own column only when no variants are loaded AND no currency was asked for, so
            // a caller from before specs/020 still reports the number it always did - never a price in
            // a currency nobody set.
            prices.Count > 0 ? prices.Min() : price ? null : p.Price,
            ProductAvailability.From(p.Availability),
            p.Sku,
            p.CategoryId,
            p.IsActive,
            Images.ProductImageKey.UrlFor(p),
            prices.Distinct().Count() > 1,
            active.Count,
            withVariants
                ? p.Variants.Select(v => VariantResponse.From(v, language, currency, defaultCurrency)).ToList()
                : null,
            localise ? Localized.LanguageOf(p, language, defaultLanguage) : string.Empty,
            currency,
            p.SellerId,
            // Null means the shop itself - which is every product listed before sellers existed, and
            // anything an administrator lists. The storefront words that; Catalog does not invent a
            // name for it (specs/027).
            sellerName);
    }
}
