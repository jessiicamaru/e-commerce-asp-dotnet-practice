namespace Ecommerce.Cart.Domain.Entities;

/// <summary>
/// A product and a quantity. Deliberately nothing else: no name, no price. Both are Catalog's.
/// </summary>
public class CartLine
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }

    /// <summary>Unique within a cart, so adding the same product again raises the quantity.</summary>
    /// <summary>Which product the line links to. Kept: it is what a storefront link points at.</summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Which SHAPE of that product is in the cart (specs/020) - the thing that carries the price and
    /// the stock. Null on a line written before variants existed, and then the product's id is its only
    /// variant's id, which is what <see cref="SellableId"/> falls back to.
    /// </summary>
    public Guid? VariantId { get; set; }

    /// <summary>What this line actually is, for matching, pricing and removing: the variant.</summary>
    public Guid SellableId => VariantId ?? ProductId;

    /// <summary>Always above zero. A line at zero is a line that no longer exists.</summary>
    public int Quantity { get; set; }

    public DateTime AddedAt { get; set; }
}
