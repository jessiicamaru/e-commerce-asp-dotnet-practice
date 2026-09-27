namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What became of a request to cancel one part of an order (specs/104).</summary>
public enum PartCancelOutcome
{
    /// <summary>Cancelled now; what goes with it was staged in the same transaction.</summary>
    Cancelled,

    /// <summary>It already was - a repeat, answered as a no-op. Nothing was published again.</summary>
    AlreadyCancelled,

    /// <summary>No such order, or no such part on it.</summary>
    NotFound,

    /// <summary>Still settling, or failed.</summary>
    NotPaid,

    /// <summary>The part has shipped: it is a return now, not a cancellation.</summary>
    Shipped,

    /// <summary>The whole order was cancelled already.</summary>
    OrderCancelled
}

/// <summary>
/// The part just cancelled, for its stage: what it refunds and which variants go back - or, when
/// <paramref name="Last"/>, that nothing is left and the order was cancelled whole (research D2).
/// </summary>
/// <param name="Refund">The part's goods less their discounts, plus their tax - the sum a received return refunds.</param>
public record CancelledPart(
    Guid OrderId, Guid PartId, Guid? SellerId, bool Last, decimal Refund, string Currency, List<Guid> VariantIds);

/// <summary>The words, and who can cancel a part.</summary>
public static class PartCancellation
{
    public const string BySeller = "Seller";
    public const string ByStaff = "Staff";

    public const string Shipped = "This part has been shipped; it can no longer be cancelled - the buyer can return it.";
    public const string NoShopPart = "This order has no part the shop ships; each seller cancels their own.";
    public const string Cancelled = "This part was cancelled.";
}
