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
    /// </remarks>
    public static readonly OrderStatus[] Statuses =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped];

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
    string Currency);

/// <summary>
/// One sale: the caller's own lines of one order, and nothing that describes the rest of it.
/// </summary>
/// <remarks>
/// ⚠️ <b>Deliberately absent</b> (research D4), and <c>SellerSalesTests</c> fails if one appears: the
/// customer, the delivery address, the order's total, delivery and tax totals, the tracking reference,
/// and every line that is not the caller's. A seller who only looks has no use for them; the address
/// arrives with the job of shipping, which is not this feature.
/// </remarks>
public record SaleDetailResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrderItemDetailResponse> Items,
    decimal Subtotal,
    string Currency,
    string Language);
