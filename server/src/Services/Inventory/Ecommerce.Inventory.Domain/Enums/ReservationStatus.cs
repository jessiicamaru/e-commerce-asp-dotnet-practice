namespace Ecommerce.Inventory.Domain.Enums;

public enum ReservationStatus
{
    /// <summary>Units are set aside for an order that has not yet settled.</summary>
    Held = 1,

    /// <summary>The order failed; the units went back to available.</summary>
    Released = 2,

    /// <summary>The order completed; the units left stock permanently.</summary>
    Confirmed = 3,

    /// <summary>Neither settlement arrived in time; the sweeper reclaimed the units.</summary>
    Expired = 4
}
