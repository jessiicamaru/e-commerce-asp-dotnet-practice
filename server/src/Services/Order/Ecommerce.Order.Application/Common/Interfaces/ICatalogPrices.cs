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
    Task<IReadOnlyList<CatalogPrice>> GetPricesAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One product, as the catalogue described it at the moment of the sale.
/// </summary>
/// <param name="Sellable">
/// Whether it can be bought at all. Not a failure — an answer. The caller decides.
/// </param>
/// <param name="ProductId">What the variant is a shape of - copied onto the line for links and reports.</param>
/// <param name="VariantId">The sellable unit. For a product that existed before variants, the same id.</param>
public record CatalogPrice(
    Guid ProductId,
    string Name,
    decimal Price,
    bool Sellable,
    Guid VariantId = default,
    string Sku = "",
    string OptionSummary = "");
