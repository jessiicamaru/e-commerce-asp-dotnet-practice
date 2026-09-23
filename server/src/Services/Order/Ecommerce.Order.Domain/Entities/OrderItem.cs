namespace Ecommerce.Order.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;

    /// <summary>
    /// Which shape of the product was bought (specs/020). Null on lines written before variants
    /// existed - those are the product's only variant, whose id is the product's own.
    /// </summary>
    public Guid? VariantId { get; set; }

    /// <summary>The variant's sku, FROZEN: what the warehouse picks, whatever the catalogue does later.</summary>
    public string? Sku { get; set; }

    /// <summary>
    /// What the customer chose, in words - <c>Kit: With 24-105mm · Colour: Black</c> - frozen for the
    /// same reason as the name and the price: an order has to keep describing itself.
    /// </summary>
    public string? OptionSummary { get; set; }
    /// <summary>
    /// Whose product this was when it was bought (specs/034), FROZEN like the name and the price. Null:
    /// the shop's own, a line from before sellers were recorded, or a Catalog that could not say.
    /// </summary>
    /// <remarks>
    /// ⚠️ Frozen on purpose, the opposite of how Inventory asks who owns a variant (specs/031, live and
    /// never cached). That is a permission on a thing NOW; this is a record of a sale THEN. Looking it
    /// up live would hand a sale to whoever owns the product today, and lose it when it is deleted.
    /// </remarks>
    public Guid? SellerId { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    /// <summary>Tax charged on this line, as rounded at checkout (feature 012). Null on older lines.</summary>
    public decimal? TaxAmount { get; set; }

    // Navigation property
    public Order? Order { get; set; }
}
