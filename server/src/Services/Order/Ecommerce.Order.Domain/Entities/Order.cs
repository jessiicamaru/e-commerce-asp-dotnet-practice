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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}
