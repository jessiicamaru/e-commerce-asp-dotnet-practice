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

    /// <summary>
    /// The marketplace's commission rate when this order was placed (specs/037), e.g. 0.1000. Stored so a
    /// rate changed later never alters what a seller earned on it. Null on orders placed before that.
    /// </summary>
    public decimal? CommissionRate { get; set; }

    /// <summary>Who cancelled it (specs/039): "Customer" or "Staff". Null unless cancelled.</summary>
    public string? CancelledBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The language this order was placed in, and therefore the language of the words frozen on its
    /// lines (specs/021). Null on orders placed before that, which read as the shop's default.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The currency this order was placed in, and therefore the currency of <b>every</b> amount on it -
    /// the total, the parts, and each line (specs/022). Null on orders placed before that, which read
    /// as the shop's default.
    /// </summary>
    /// <remarks>
    /// Nullable rather than defaulted, deliberately. An order written last week recorded amounts whose
    /// currency nobody stated, and writing <c>VND</c> into it now would be inventing a fact rather than
    /// recording one. Reads present null as the default and say which that is.
    /// </remarks>
    public string? Currency { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    /// <summary>
    /// One part per seller whose goods this order holds, plus the shop's own (specs/035). Written with
    /// the order at checkout; created on demand for an order written by an image that predates them.
    /// </summary>
    public List<OrderShipment> Shipments { get; set; } = new();

    /// <summary>The vouchers used on this order (specs/069), frozen with it: code, name and what each took off.</summary>
    public List<VoucherRedemption> Vouchers { get; set; } = new();
}
