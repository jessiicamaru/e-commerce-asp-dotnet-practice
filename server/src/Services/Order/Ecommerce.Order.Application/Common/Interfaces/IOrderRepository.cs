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

    /// <summary>
    /// One order with its items, <b>scoped to its owner</b>. The owner is part of the query rather
    /// than a check applied afterwards, so there is no state in which a caller's order id has
    /// selected somebody else's row. A caller asking for an order that is not theirs and a caller
    /// asking for one that does not exist both get <c>null</c>, which is what makes the two
    /// indistinguishable from outside.
    /// </summary>
    /// <summary>
    /// Moves an order one fulfilment step: <c>UPDATE ... WHERE "Id" = @id AND "Status" = @from</c>.
    /// Returns the rows changed; zero means it was not in <paramref name="from"/> - already moved, moved
    /// further, failed, or not there. The caller re-reads to tell which. The guard is in the statement
    /// so two staff clicks cannot both succeed (Constitution III).
    /// </summary>
    /// <remarks>
    /// Moving from <see cref="OrderStatus.Paid"/> also accepts a legacy <see cref="OrderStatus.Completed"/>
    /// row, which means the same thing.
    /// </remarks>
    Task<int> TryAdvanceAsync(
        Guid orderId,
        OrderStatus from,
        OrderStatus to,
        string? trackingReference,
        DateTime at,
        CancellationToken cancellationToken = default);

    /// <summary>A page of every customer's orders in one status, oldest first - staff work a queue.</summary>
    Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByStatusAsync(
        OrderStatus status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Order?> GetByIdForUserAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
