namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What happened when a customer said a parcel arrived (specs/040).</summary>
public enum DeliveryConfirmOutcome
{
    /// <summary>Recorded now.</summary>
    Confirmed,

    /// <summary>It already was - by the customer before, or automatically. A no-op.</summary>
    AlreadyDelivered,

    /// <summary>The parcel has not been shipped: there is nothing to have received.</summary>
    NotShipped,

    /// <summary>No such order or parcel, or not the caller's.</summary>
    NotFound
}

/// <summary>Who confirmed a delivery, and the words for each refusal.</summary>
public static class ParcelDelivery
{
    public const string ByCustomer = "Customer";
    public const string ByAuto = "Auto";

    public const string NotFound = "Order not found.";
    public const string NotShipped = "This parcel has not been shipped yet.";
}
