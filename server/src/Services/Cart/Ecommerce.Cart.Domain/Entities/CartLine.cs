namespace Ecommerce.Cart.Domain.Entities;

/// <summary>
/// A product and a quantity. Deliberately nothing else: no name, no price. Both are Catalog's.
/// </summary>
public class CartLine
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }

    /// <summary>Unique within a cart, so adding the same product again raises the quantity.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Always above zero. A line at zero is a line that no longer exists.</summary>
    public int Quantity { get; set; }

    public DateTime AddedAt { get; set; }
}
