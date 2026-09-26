namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// A product one shopper saved for later (specs/075, #109). One row per shopper per product; the shopper is the
/// token's subject, never the request's. Goes with the product when the product is deleted.
/// </summary>
public class SavedProduct
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime SavedAt { get; set; }

    public Product? Product { get; set; }
}
