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
/// </remarks>
public record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Availability,
    string Sku,
    Guid CategoryId,
    bool IsActive
);
