namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// What the catalogue charges for a product, asked of the service that owns the price.
/// </summary>
/// <remarks>
/// <para>
/// Order used to take the unit price from the customer's own request. Issue #18: a product listed
/// at 40,000,000 was bought for 1, the stock was permanently deducted, and the payment recorded
/// <c>1.00 Approved</c>. None of the sixty-one tests could see it, because each supplied its own
/// price and then asserted against that same number.
/// </para>
/// <para>
/// <b>This interface carries no transport.</b> The Application layer depends on abstractions only —
/// never a transport package — so the gRPC client lives in Infrastructure behind this. Swapping
/// gRPC for REST later touches one file, and <c>grep Grpc</c> over this project must stay empty.
/// </para>
/// </remarks>
public interface ICatalogPrices
{
    /// <summary>
    /// Prices every product in one call, or refuses the lot.
    /// </summary>
    /// <remarks>
    /// One call per order, never one per line. Per-line calls leave the caller holding partial
    /// state when the third line fails, and turn "refuse the order in full" into something the
    /// caller must remember rather than something the shape guarantees.
    /// </remarks>
    /// <exception cref="Shared.Exceptions.NotFoundException">
    /// At least one product id is not in the catalogue.
    /// </exception>
    /// <exception cref="Shared.Exceptions.DependencyUnavailableException">
    /// The catalogue could not be asked. <b>Distinct from not-found on purpose</b>: "this product
    /// does not exist" and "I could not find out whether it exists" are different facts, and a
    /// customer told the first when the second is true goes and checks a catalogue that is fine.
    /// </exception>
    /// <summary>
    /// Prices the SELLABLE units being bought - variants since specs/020. All-or-nothing: an unknown
    /// one refuses the whole order rather than pricing the rest.
    /// </summary>
    /// <param name="language">
    /// Which language to answer in (specs/021). The name and options that come back are FROZEN onto
    /// the order, so this decides the words the order keeps. Empty means the shop's default.
    /// </param>
    /// <param name="currency">
    /// Which currency to price in (specs/022). The amount that comes back is FROZEN onto the order,
    /// so this decides the money the order records. Empty means the shop's default.
    /// <b>A variant with no price in it comes back with a null price and <c>Sellable = false</c></b> -
    /// never converted, and never the default currency's amount relabelled.
    /// </param>
    Task<IReadOnlyList<CatalogPrice>> GetPricesAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default,
        string language = "",
        string currency = "");
}

/// <summary>
/// One product, as the catalogue described it at the moment of the sale.
/// </summary>
/// <param name="Sellable">
/// Whether it can be bought at all. Not a failure — an answer. The caller decides.
/// </param>
/// <param name="ProductId">What the variant is a shape of - copied onto the line for links and reports.</param>
/// <param name="VariantId">The sellable unit. For a product that existed before variants, the same id.</param>
/// <param name="Price">
/// What it costs in <paramref name="Currency"/>, or <b>null when it is not sold in that currency</b>
/// (specs/022). A null price always comes with <c>Sellable = false</c>, so a caller that reads only
/// the flag cannot charge a blank.
/// </param>
/// <param name="Currency">Which currency <paramref name="Price"/> is in, as Catalog echoed it back.</param>
public record CatalogPrice(
    Guid ProductId,
    string Name,
    decimal? Price,
    bool Sellable,
    Guid VariantId = default,
    string Sku = "",
    string OptionSummary = "",
    string Currency = "");
