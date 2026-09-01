namespace Ecommerce.Catalog.Application.Products.Common;

public record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string Sku,
    Guid CategoryId,
    bool IsActive
);
