namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What happened when an order was asked to be cancelled (specs/039).</summary>
public enum CancelOutcome
{
    /// <summary>It was cancelled now, and the event that undoes it elsewhere was staged with it.</summary>
    Cancelled,

    /// <summary>It already was - a repeat, answered as a no-op. Nothing was published again.</summary>
    AlreadyCancelled,

    /// <summary>No such order, or - for a customer - not theirs.</summary>
    NotFound,

    /// <summary>Still settling, or failed: there is nothing to undo yet, or already nothing.</summary>
    NotPaid,

    /// <summary>A parcel is being prepared, and the caller is the customer (staff may still cancel).</summary>
    BeingPrepared,

    /// <summary>A parcel has shipped. Nobody can cancel.</summary>
    Shipped
}

/// <summary>The words for each refusal, shared by the customer's and the staff's commands.</summary>
public static class Cancellation
{
    public const string ByCustomer = "Customer";
    public const string ByStaff = "Staff";

    public const string NotFound = "Order not found.";
    public const string NotPaid = "Only a paid order can be cancelled.";
    public const string BeingPrepared = "This order is being prepared; ask the shop to cancel it.";
    public const string Shipped = "Part of this order has been shipped; it can no longer be cancelled.";
}
