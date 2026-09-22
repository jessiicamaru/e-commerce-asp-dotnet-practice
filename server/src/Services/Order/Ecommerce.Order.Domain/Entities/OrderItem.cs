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
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    /// <summary>Tax charged on this line, as rounded at checkout (feature 012). Null on older lines.</summary>
    public decimal? TaxAmount { get; set; }

    // Navigation property
    public Order? Order { get; set; }
}
