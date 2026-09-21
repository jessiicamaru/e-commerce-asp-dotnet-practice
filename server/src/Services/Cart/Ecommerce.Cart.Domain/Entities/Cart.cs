namespace Ecommerce.Cart.Domain.Entities;

/// <summary>
/// What one customer intends to buy. One per customer, long-lived, freely changed.
/// </summary>
/// <remarks>
/// <b>It holds no price.</b> Catalog owns the price, and a copy kept here would go stale silently and
/// could be mistaken for something to charge. The constitution forbids a non-owning copy informing a
/// charge/refuse decision, and the simplest way to obey that is to have nothing to misuse. Prices are
/// asked of Catalog when the cart is read. See specs/010-customer-cart research D5.
/// </remarks>
public class Cart
{
    public Guid Id { get; set; }

    /// <summary>From the validated token, never from a request. Unique: one cart per customer.</summary>
    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CartLine> Lines { get; set; } = [];
}
