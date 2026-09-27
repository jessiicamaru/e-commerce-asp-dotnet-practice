using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Domain.Entities;

namespace Ecommerce.Inventory.Application.Stock.Common;

/// <param name="LowStockThreshold">The line in force (specs/102): the variant's own, or the shop's default.</param>
/// <param name="LowStockThresholdIsDefault">Whether that is the shop's default rather than a choice.</param>
public record StockResponse(
    Guid ProductId,
    string Sku,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    int LowStockThreshold,
    bool LowStockThresholdIsDefault)
{
    public static StockResponse From(StockItem x, LowStockSettings lowStock) => new(
        x.ProductId, x.Sku, x.QuantityOnHand, x.QuantityReserved, x.QuantityAvailable,
        lowStock.For(x), x.LowStockThreshold is null);
}
