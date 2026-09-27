namespace Ecommerce.Contracts.Inventory;

/// <summary>
/// A sale took a variant's available stock from at or above its low-stock line to below it (#200, specs/102). Published by
/// Inventory in the reservation's own transaction, once per crossing; Catalog tells the product's seller.
/// </summary>
/// <param name="VariantId">The variant - what <c>stock_items.ProductId</c> holds since specs/020.</param>
/// <param name="QuantityAvailable">What is left to sell after the sale.</param>
/// <param name="Threshold">The line it went below.</param>
public record StockRanLowEvent(Guid VariantId, int QuantityAvailable, int Threshold, DateTime OccurredAt);
