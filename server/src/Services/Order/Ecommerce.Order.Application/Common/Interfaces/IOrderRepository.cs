using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task<Domain.Entities.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Domain.Entities.Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an order from <see cref="OrderStatus.Submitted"/> to a settled status, and returns the
    /// number of rows that changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zero is a normal answer, not a failure.</b> It means the order has already settled, or is
    /// not held by this service at all — both of which happen during ordinary operation, because the
    /// broker redelivers.
    /// </para>
    /// <para>
    /// The source status is part of the statement the database executes, not something the caller
    /// checks first. Reading the order, testing its status and then writing would let two concurrent
    /// deliveries both observe <c>Submitted</c> and both write; here the second affects nothing.
    /// This is what the constitution's principle III asks for in so many words: a repeated attempt
    /// affects zero rows rather than applying the effect twice.
    /// </para>
    /// </remarks>
    Task<int> TrySettleAsync(
        Guid orderId,
        OrderStatus settledStatus,
        string? failureReason,
        DateTime settledAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this service holds the order at all. Used only to explain a zero-row settle: an order
    /// that exists was already settled, one that does not is a message for somebody else's database.
    /// The two deserve different log levels, and an operator reading one line cannot tell them apart.
    /// </summary>
    Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of a single shopper's orders, newest first, with the total count of that shopper's
    /// orders. Returns summaries rather than entities because the line items are deliberately
    /// <b>not</b> loaded — the list reports how many items an order has, not which, so its cost
    /// grows with the page size and not with the size of the orders on it.
    /// </summary>
    Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>A page of every customer's orders in one status, oldest first - staff work a queue.</summary>
    Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByStatusAsync(
        OrderStatus status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of the orders holding at least one of this seller's lines, newest first, counting only
    /// <see cref="Sales.Statuses"/> (specs/034). Every figure on a row is over the seller's lines only.
    /// </summary>
    Task<(List<SaleSummaryResponse> Sales, int TotalCount)> GetSalesPageAsync(
        Guid sellerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One sale with <b>only this seller's lines</b>, or null. The seller, the status and the order id
    /// are all in the one query, so an order that is somebody else's sale is never read and then
    /// refused - it is simply not found, exactly like one that does not exist.
    /// </summary>
    Task<SaleDetailResponse?> GetSaleAsync(
        Guid orderId,
        Guid sellerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves ONE part of an order one step - <paramref name="sellerId"/>'s, or the shop's when null -
    /// and rewrites the order's summary status in the same transaction (specs/035).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order's row is locked FIRST. Two sellers shipping the last two parts at once would otherwise
    /// each compute the summary from a snapshot in which the other's part is not shipped yet, and the
    /// order would stay Preparing for good (research D5). The lock serialises moves per order only.
    /// </para>
    /// <para>
    /// Parts missing from the order - one written by an image that predates them - are created first,
    /// in the state the order is in (research D3). The part's own move is a guarded single UPDATE: a
    /// repeat affects no row and comes back <see cref="ShipmentMoveOutcome.AlreadyThere"/>.
    /// </para>
    /// </remarks>
    Task<ShipmentMoveResult> TryMoveShipmentAsync(
        Guid orderId,
        Guid? sellerId,
        ShipmentStatus from,
        ShipmentStatus to,
        string? trackingReference,
        DateTime at,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a paid order, if it may be cancelled, and stages the event that undoes it elsewhere - in ONE
    /// transaction under the same row lock as every parcel move (specs/039 research D2), so a cancel and a
    /// ship on one order run one after the other and exactly one of them wins.
    /// </summary>
    /// <param name="ownerId">The customer; null for staff. Someone else's order is <see cref="CancelOutcome.NotFound"/>.</param>
    /// <param name="allowWhilePreparing">Staff may cancel while a parcel is being prepared; a customer may not.</param>
    /// <param name="stage">
    /// Publishes the event. Called only when the order was cancelled by THIS call, between the guarded
    /// UPDATE and the save, so the outbox message commits with the row.
    /// </param>
    Task<CancelOutcome> TryCancelAsync(
        Guid orderId,
        Guid? ownerId,
        bool allowWhilePreparing,
        string cancelledBy,
        DateTime at,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that one parcel of the owner's order arrived (specs/040): one guarded statement, so a repeat,
    /// or a sweep at the same moment, sets it once. The owner is part of the query.
    /// </summary>
    Task<DeliveryConfirmOutcome> TryConfirmDeliveryAsync(
        Guid orderId,
        Guid shipmentId,
        Guid ownerId,
        DateTime at,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes every parcel shipped before <paramref name="shippedBefore"/> and not yet confirmed as delivered,
    /// "Auto" (specs/040). One statement; returns how many it changed.
    /// </summary>
    Task<int> AutoConfirmDeliveriesAsync(DateTime shippedBefore, DateTime at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates any part the order is missing, in the order's state, and nothing else. Idempotent:
    /// <c>INSERT … ON CONFLICT DO NOTHING</c> against the unique (order, seller) index.
    /// </summary>
    Task EnsureShipmentsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One order with its items, <b>scoped to its owner</b>. The owner is part of the query rather
    /// than a check applied afterwards, so there is no state in which a caller's order id has
    /// selected somebody else's row. A caller asking for an order that is not theirs and a caller
    /// asking for one that does not exist both get <c>null</c>, which is what makes the two
    /// indistinguishable from outside.
    /// </summary>
    Task<Domain.Entities.Order?> GetByIdForUserAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
