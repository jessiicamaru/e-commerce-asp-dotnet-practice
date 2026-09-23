using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What counts as a sale (specs/034 research D3).</summary>
public static class Sales
{
    /// <summary>
    /// Paid, and anything a paid order moves on to. A legacy <see cref="OrderStatus.Completed"/> row is
    /// a paid order by its old name (feature 011).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Not <c>Failed</c>, and not <c>Submitted</c>.</b> A failed order was never a sale; showing it
    /// would tell a seller they sold something and then take it back. <c>Submitted</c> settles in
    /// seconds, and showing it would do the same whenever the payment is then declined.
    /// <para>
    /// Since specs/039 a <b>cancelled</b> order is a sale too - its seller must learn to stop preparing it -
    /// but it is never money: balances and payouts count <see cref="Earning"/>, not this.
    /// </para>
    /// </remarks>
    public static readonly OrderStatus[] Statuses =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped, OrderStatus.Cancelled];

    /// <summary>
    /// The sales that are money (specs/037, 039): paid and not cancelled. ⚠️ The only thing keeping a
    /// cancelled order - whose parts still exist, with their terms - out of a balance and out of a payout.
    /// </summary>
    public static readonly OrderStatus[] Earning =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped];

    /// <summary>Moving a cancelled sale of one's own (specs/039).</summary>
    public const string Cancelled = "This order was cancelled.";

    /// <summary>
    /// One wording for "no such order", "not your sale", "failed" and "still settling" (research D5).
    /// A second sentence for any of them would confirm that somebody else sold something on that order.
    /// </summary>
    public const string NotFound = "Sale not found.";
}

/// <summary>One row of a seller's sales: an order holding at least one of their lines.</summary>
/// <param name="LineCount">How many of the order's lines are the caller's.</param>
/// <param name="Units">How many units those lines hold between them.</param>
/// <param name="Subtotal">
/// <c>Σ unit price × quantity</c> over the caller's lines, before tax, in <paramref name="Currency"/>.
/// </param>
/// <param name="Payout">
/// What the shop owes the caller for this sale: <c>GoodsTotal - Commission + ShippingShare</c> (specs/037).
/// All four are null when the terms were not recorded - an order from before that.
/// </param>
/// <param name="PaidOut">Whether a payout has covered it.</param>
/// <remarks>
/// ⚠️ <b>No order total</b>, on purpose (research D4): on an order mixing sellers it includes goods that
/// are not the caller's, and delivery and tax are computed over the whole order.
/// </remarks>
public record SaleSummaryResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int LineCount,
    int Units,
    decimal Subtotal,
    string Currency,
    decimal? GoodsTotal = null,
    decimal? Commission = null,
    decimal? ShippingShare = null,
    decimal? Payout = null,
    bool PaidOut = false);

/// <summary>
/// One sale: the caller's own lines of one order, and nothing that describes the rest of it.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Deliberately absent</b> (specs/034 research D4), and <c>SellerSalesTests</c> fails if one
/// appears: the customer's id or email, the order's total, delivery and tax totals, and every line that
/// is not the caller's.
/// </para>
/// <para>
/// <paramref name="Status"/> and <paramref name="TrackingReference"/> are THEIR part's (specs/035), not
/// the order's: another seller's parcel being sent says nothing about theirs.
/// <paramref name="ShippingAddress"/> is present <b>only while their part is waiting or being
/// prepared</b> - the address arrives with the job of shipping and leaves when the job is done
/// (specs/035 research D6).
/// </para>
/// </remarks>
public record SaleDetailResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrderItemDetailResponse> Items,
    decimal Subtotal,
    string Currency,
    string Language,
    string? TrackingReference = null,
    ShippingAddressResponse? ShippingAddress = null,
    decimal? GoodsTotal = null,
    decimal? Commission = null,
    decimal? ShippingShare = null,
    decimal? Payout = null,
    bool PaidOut = false);
