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
    Task<CatalogDescription> DescribeAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
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

public record CatalogProduct(Guid ProductId, string Name, decimal Price, bool Sellable);
