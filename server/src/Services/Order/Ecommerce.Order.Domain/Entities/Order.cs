using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? FailureReason { get; set; }

    // Feature 011 - where it goes and how. All null on orders placed before it: they recorded no
    // destination, and are not given an invented one.
    public ShippingAddress? ShipTo { get; set; }
    public string? ShippingOptionCode { get; set; }
    public string? ShippingOptionName { get; set; }
    public decimal? ShippingPrice { get; set; }
    public string? TrackingReference { get; set; }

    // Feature 012 - the parts of the total, fixed at checkout. Subtotal + ShippingPrice + TaxTotal
    // - DiscountTotal = TotalAmount, enforced by a CHECK constraint. Null only on rows written by an
    // image from before this feature (the constraint lets those through; see specs/012 research D4).
    public decimal? Subtotal { get; set; }
    public decimal? TaxTotal { get; set; }
    public decimal? DiscountTotal { get; set; }

    /// <summary>The rate applied, e.g. 0.1000 for 10%. Stored so a later rate change never alters this order.</summary>
    public decimal? TaxRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The language this order was placed in, and therefore the language of the words frozen on its
    /// lines (specs/021). Null on orders placed before that, which read as the shop's default.
    /// </summary>
    public string? Language { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}
