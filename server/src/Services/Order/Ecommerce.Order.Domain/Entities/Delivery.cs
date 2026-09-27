namespace Ecommerce.Order.Domain.Entities;

/// <summary>
/// A way an order can be sent, and what it costs per currency (#196, specs/098). Administrators edit it; configuration
/// (<c>Shipping:Options</c>) only seeds codes the table does not have. An order freezes the name and price it chose at
/// checkout (specs/011), so an edit here never changes an order already placed.
/// </summary>
public class DeliveryOption
{
    /// <summary>What checkout sends: lower-case letters, digits and hyphens. Fixed once created.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Offered to shoppers. An option is turned off, never deleted - orders name it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The order shoppers see the options in, lowest first.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>One price per currency. No row, not offered in that currency - never converted (specs/022).</summary>
    public List<DeliveryOptionPrice> Prices { get; set; } = [];
}

public class DeliveryOptionPrice
{
    public string OptionCode { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// The shop's one delivery partner (decided with the user, specs/098): its name, and the address of its tracking page as a
/// template with <c>{reference}</c> where the parcel's tracking reference goes. One row, id 1.
/// </summary>
public class Carrier
{
    public const int TheCarrier = 1;

    public int Id { get; set; } = TheCarrier;
    public string Name { get; set; } = string.Empty;
    public string? TrackingUrlTemplate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
