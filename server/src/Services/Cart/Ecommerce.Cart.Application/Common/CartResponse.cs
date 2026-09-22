namespace Ecommerce.Cart.Application.Common;

/// <param name="EstimatedTotal">
/// An ESTIMATE, and named so. The amount charged is decided at checkout by Catalog; a price shown here
/// can change before then.
/// </param>
/// <param name="CanCheckOut">
/// False while any line cannot be bought, so a client can say why before the customer tries.
/// </param>
/// <param name="PricesAvailable">False when Catalog could not be reached. The lines are still here.</param>
public record CartResponse(
    List<CartLineResponse> Lines,
    decimal? EstimatedTotal,
    bool CanCheckOut,
    bool PricesAvailable);

/// <param name="VariantId">Which shape of the product this line is (specs/020).</param>
/// <param name="OptionSummary">That shape in words - empty for a product sold one way.</param>
public record CartLineResponse(
    Guid ProductId,
    string? Name,
    int Quantity,
    decimal? UnitPrice,
    decimal? LineTotal,
    string Status,
    Guid VariantId = default,
    string OptionSummary = "");

/// <summary>Why a line can or cannot be bought.</summary>
public static class CartLineStatus
{
    public const string Available = "Available";
    public const string NotForSale = "NotForSale";

    /// <summary>
    /// The product is gone. The line is KEPT and marked rather than silently dropped - a cart that
    /// shrinks without explanation is the worst of the options.
    /// </summary>
    public const string NoLongerAvailable = "NoLongerAvailable";

    public const string PriceUnavailable = "PriceUnavailable";
}
