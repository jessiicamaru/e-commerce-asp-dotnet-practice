namespace Ecommerce.Contracts.Catalog;

public record ProductCreatedEvent(
    Guid ProductId,
    string Name,
    decimal Price,
    string Sku,
    Guid CategoryId,
    DateTime CreatedAt
);
