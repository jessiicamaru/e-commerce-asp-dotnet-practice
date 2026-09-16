namespace Ecommerce.Inventory.Application.Stock.Common;

public record StockResponse(
    Guid ProductId,
    string Sku,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable
);
