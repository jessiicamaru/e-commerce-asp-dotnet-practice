namespace Ecommerce.Cart.Application.Common.Interfaces;

/// <summary>
/// What products look like right now, for SHOWING a cart. Never for charging.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a different question from the one checkout asks. Checkout calls Catalog's GetPrices,
/// which refuses everything if a single product is missing - correct when deciding a sale. A cart
/// being displayed needs the opposite: the missing line marked, and the rest answered. So this calls
/// DescribeProducts, which never fails the whole request over one product.
/// </para>
/// <para>
/// No transport here. The gRPC client lives in Infrastructure; the Application layer depends on
/// abstractions only.
/// </para>
/// </remarks>
public interface ICatalogProducts
{
    /// <summary>
    /// Describes the SELLABLE units in a cart - variants since specs/020. The ids are what the cart
    /// lines hold, and for a line written before variants that is the product's own id, which is also
    /// its only variant's.
    /// </summary>
    Task<CatalogDescription> DescribeAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);
}

/// <param name="Reachable">
/// False when Catalog could not be asked. The cart is still shown - what a customer chose is the
/// cart's own data - only the prices are unavailable.
/// </param>
public record CatalogDescription(
    bool Reachable,
    IReadOnlyList<CatalogProduct> Products,
    IReadOnlyList<Guid> Missing)
{
    public static CatalogDescription Unreachable() => new(false, [], []);
}

/// <param name="ProductId">What the line links to; a variant is a shape OF a product.</param>
/// <param name="OptionSummary">What the customer chose, in words: <c>Kit: Body only</c>. Empty for a product sold one way.</param>
public record CatalogProduct(
    Guid ProductId,
    string Name,
    decimal Price,
    bool Sellable,
    Guid VariantId = default,
    string OptionSummary = "");
