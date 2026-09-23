using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

public class OrderRepository(OrderDbContext context) : IOrderRepository
{
    private readonly OrderDbContext _context = context;

    public async Task<Domain.Entities.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(x => x.Items)
            .Include(x => x.Shipments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Domain.Entities.Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(x => x.Items)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One statement:
    /// <code>
    /// UPDATE orders
    ///    SET "Status" = @settled, "FailureReason" = @reason, "UpdatedAt" = @at
    ///  WHERE "Id" = @orderId AND "Status" = 'Submitted'
    /// </code>
    /// The guard is in the WHERE clause on purpose. Do not "improve" this by loading the order and
    /// testing its status first — that reintroduces the race the guard exists to close, and the
    /// window is small enough that no test would notice.
    /// </summary>
    public async Task<int> TrySettleAsync(
        Guid orderId,
        OrderStatus settledStatus,
        string? failureReason,
        DateTime settledAt,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(x => x.Id == orderId && x.Status == OrderStatus.Submitted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, settledStatus)
                    .SetProperty(x => x.FailureReason, failureReason)
                    .SetProperty(x => x.UpdatedAt, settledAt),
                cancellationToken);
    }

    public async Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByStatusAsync(
        OrderStatus status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var alsoStatus = status == OrderStatus.Paid ? OrderStatus.Completed : status;
        var part = status switch
        {
            OrderStatus.Preparing => ShipmentStatus.Preparing,
            OrderStatus.Shipped => ShipmentStatus.Shipped,
            _ => ShipmentStatus.Pending
        };

        // Staff work the SHOP's part (specs/035): an order whose shop parcel is sent is not in their
        // "Preparing" queue because a seller's parcel on it is not. An order with no parts at all - one
        // an older image wrote - is placed by its own status, as before. Only paid orders: a part is
        // Pending on a submitted or failed order too, and neither is anybody's work.
        var query = _context.Orders
            .AsNoTracking()
            .Where(x => Payable.Contains(x.Status))
            .Where(x => x.Shipments.Any(s => s.SellerId == null && s.Status == part)
                || (!x.Shipments.Any() && (x.Status == status || x.Status == alsoStatus)));

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.TotalAmount,
                x.Status,
                x.FailureReason,
                ItemCount = x.Items.Count,
                x.CreatedAt,
                x.UpdatedAt,
                // Frozen at checkout: a list of orders must label each amount with the money it was
                // charged in, not with whatever the reader is browsing in (specs/022).
                x.Currency,
                x.Language,
                // "1 of 2 parcels shipped" (specs/035) - in SQL, as correlated counts.
                ShipmentCount = x.Shipments.Count,
                ShipmentsShipped = x.Shipments.Count(s => s.Status == ShipmentStatus.Shipped)
            })
            .ToListAsync(cancellationToken);

        var orders = rows.Select(x => new OrderSummaryResponse(
            x.Id, x.TotalAmount, OrderMapping.Describe(x.Status), x.FailureReason, x.ItemCount, x.CreatedAt,
            x.UpdatedAt, x.Currency ?? string.Empty, x.Language ?? string.Empty, x.ShipmentCount, x.ShipmentsShipped))
            .ToList();

        return (orders, totalCount);
    }

    public async Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders.AnyAsync(x => x.Id == orderId, cancellationToken);
    }

    public async Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        // Counted before paging, so the caller learns the true total even when they have asked for
        // a page past the end — which is how they find out they overshot.
        var totalCount = await query.CountAsync(cancellationToken);

        // Projected in SQL, not loaded and mapped. The item count becomes a correlated COUNT, so a
        // page of ten orders holding a hundred lines between them still transfers ten rows.
        //
        // Status stays an enum here and is turned into a string below. Projecting
        // `x.Status.ToString()` would read more directly, but whether it translates depends on the
        // provider's handling of a value-converted enum, and a projection that silently falls back
        // to client evaluation — or throws at runtime — is not worth the line it saves.
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.TotalAmount,
                x.Status,
                x.FailureReason,
                ItemCount = x.Items.Count,
                x.CreatedAt,
                x.UpdatedAt,
                // Frozen at checkout: a list of orders must label each amount with the money it was
                // charged in, not with whatever the reader is browsing in (specs/022).
                x.Currency,
                x.Language,
                // "1 of 2 parcels shipped" (specs/035) - in SQL, as correlated counts.
                ShipmentCount = x.Shipments.Count,
                ShipmentsShipped = x.Shipments.Count(s => s.Status == ShipmentStatus.Shipped)
            })
            .ToListAsync(cancellationToken);

        var orders = rows.Select(x => new OrderSummaryResponse(
            x.Id,
            x.TotalAmount,
            OrderMapping.Describe(x.Status),
            x.FailureReason,
            x.ItemCount,
            x.CreatedAt,
            x.UpdatedAt,
            x.Currency ?? string.Empty,
            x.Language ?? string.Empty,
            x.ShipmentCount,
            x.ShipmentsShipped)).ToList();

        return (orders, totalCount);
    }

    public async Task<(List<SaleSummaryResponse> Sales, int TotalCount)> GetSalesPageAsync(
        Guid sellerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var statuses = Sales.Statuses;

        // Both guards in SQL: a sale is a paid order with a line of theirs. Filtering either afterwards
        // would page over rows that are then dropped, and report a total that is not the seller's.
        var query = _context.Orders
            .AsNoTracking()
            .Where(x => statuses.Contains(x.Status) && x.Items.Any(i => i.SellerId == sellerId));

        var totalCount = await query.CountAsync(cancellationToken);

        // Every figure is over THIS seller's lines only (research D4). The order's own TotalAmount is
        // not selected at all, so it cannot leak into a row by a later edit to the mapping below.
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Status,
                // THEIR part's state (specs/035), when it has one; an order an older image wrote has none.
                Part = x.Shipments.Where(s => s.SellerId == sellerId).Select(s => (ShipmentStatus?)s.Status).FirstOrDefault(),
                x.CreatedAt,
                x.UpdatedAt,
                x.Currency,
                LineCount = x.Items.Count(i => i.SellerId == sellerId),
                Units = x.Items.Where(i => i.SellerId == sellerId).Sum(i => i.Quantity),
                Subtotal = x.Items.Where(i => i.SellerId == sellerId).Sum(i => i.UnitPrice * i.Quantity)
            })
            .ToListAsync(cancellationToken);

        var sales = rows.Select(x => new SaleSummaryResponse(
            x.Id,
            x.Part is { } part ? OrderMapping.Describe(part) : OrderMapping.Describe(x.Status),
            x.CreatedAt,
            x.UpdatedAt,
            x.LineCount,
            x.Units,
            x.Subtotal,
            x.Currency ?? string.Empty)).ToList();

        return (sales, totalCount);
    }

    public async Task<SaleDetailResponse?> GetSaleAsync(
        Guid orderId,
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        var statuses = Sales.Statuses;

        // One query, three predicates. Loading the order and then checking would read another seller's
        // order - or a failed one - into memory before deciding to refuse it.
        var row = await _context.Orders
            .AsNoTracking()
            .Where(x => x.Id == orderId
                && statuses.Contains(x.Status)
                && x.Items.Any(i => i.SellerId == sellerId))
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt,
                x.Currency,
                x.Language,
                Part = x.Shipments
                    .Where(s => s.SellerId == sellerId)
                    .Select(s => new { s.Status, s.TrackingReference })
                    .FirstOrDefault(),
                x.ShipTo,
                // Only theirs. The other lines of the order are never selected.
                Items = x.Items
                    .Where(i => i.SellerId == sellerId)
                    .Select(i => new OrderItemDetailResponse(
                        i.ProductId,
                        i.ProductName,
                        i.Quantity,
                        i.UnitPrice,
                        i.UnitPrice * i.Quantity,
                        i.TaxAmount,
                        i.VariantId,
                        i.Sku,
                        i.OptionSummary))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        // Their part, or - for an order an older image wrote - the order's own state read the same way.
        var partStatus = row.Part?.Status ?? row.Status switch
        {
            OrderStatus.Shipped => ShipmentStatus.Shipped,
            OrderStatus.Preparing => ShipmentStatus.Preparing,
            _ => ShipmentStatus.Pending
        };

        return new SaleDetailResponse(
            row.Id,
            OrderMapping.Describe(partStatus),
            row.CreatedAt,
            row.UpdatedAt,
            row.Items,
            row.Items.Sum(i => i.TotalPrice),
            row.Currency ?? string.Empty,
            row.Language ?? string.Empty,
            row.Part?.TrackingReference,
            // ⚠️ Where to send it - only while sending it is their job (specs/035 research D6). Once
            // their parcel is out, a seller holding the customer's home address has no use for it.
            partStatus == ShipmentStatus.Shipped ? null : OrderMapping.ToResponse(row.ShipTo));
    }

    /// <summary>Order statuses in which a part may be worked on: the order has been paid.</summary>
    private static readonly OrderStatus[] Payable =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped];

    public async Task EnsureShipmentsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // The same rule as the AddOrderShipments backfill: one part per seller on the order, and one for
        // the shop's goods, starting in the state the ORDER is in - so an order an older image shipped is
        // a shipped part here, not a part waiting to be shipped a second time.
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO order_shipments ("Id", "OrderId", "SellerId", "Status", "TrackingReference", "UpdatedAt")
            SELECT gen_random_uuid(),
                   o."Id",
                   i."SellerId",
                   CASE o."Status" WHEN 'Preparing' THEN 'Preparing' WHEN 'Shipped' THEN 'Shipped' ELSE 'Pending' END,
                   CASE WHEN o."Status" = 'Shipped' THEN o."TrackingReference" END,
                   o."UpdatedAt"
              FROM orders o
              JOIN (SELECT DISTINCT "OrderId", "SellerId" FROM order_items WHERE "OrderId" = {orderId}) i
                ON i."OrderId" = o."Id"
            ON CONFLICT ("OrderId", "SellerId") DO NOTHING
            """, cancellationToken);
    }

    public Task<ShipmentMoveResult> TryMoveShipmentAsync(
        Guid orderId,
        Guid? sellerId,
        ShipmentStatus from,
        ShipmentStatus to,
        string? trackingReference,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        // Through the execution strategy, because production enables EnableRetryOnFailure and that
        // refuses a transaction the caller opened itself. The whole unit is retried, which is safe:
        // every write in it is guarded or ON CONFLICT DO NOTHING.
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // 1. The order's row lock, before anything is read. See the interface for why.
            var locked = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM orders WHERE "Id" = {orderId} FOR UPDATE""", cancellationToken);

            var orderStatus = await _context.Orders
                .Where(o => o.Id == orderId)
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (orderStatus is null)
            {
                return new ShipmentMoveResult(ShipmentMoveOutcome.NoSuchPart);
            }

            if (!Payable.Contains(orderStatus.Value))
            {
                return new ShipmentMoveResult(ShipmentMoveOutcome.OrderNotPaid, OrderStatus: orderStatus);
            }

            // 2. Parts an older image never wrote.
            await EnsureShipmentsAsync(orderId, cancellationToken);

            // 3. The part itself: one guarded statement. `==` on a nullable Guid is translated to
            //    "equal, or both null", so the shop's part (null) is matched as well as a seller's.
            var part = _context.OrderShipments
                .Where(s => s.OrderId == orderId && s.SellerId == sellerId && s.Status == from);

            var moved = trackingReference is null
                ? await part.ExecuteUpdateAsync(
                    x => x.SetProperty(s => s.Status, to).SetProperty(s => s.UpdatedAt, at), cancellationToken)
                : await part.ExecuteUpdateAsync(
                    x => x.SetProperty(s => s.Status, to)
                          .SetProperty(s => s.TrackingReference, trackingReference)
                          .SetProperty(s => s.UpdatedAt, at),
                    cancellationToken);

            var now = await _context.OrderShipments
                .AsNoTracking()
                .Where(s => s.OrderId == orderId && s.SellerId == sellerId)
                .Select(s => new { s.Status, s.TrackingReference })
                .FirstOrDefaultAsync(cancellationToken);

            if (moved == 0)
            {
                await transaction.CommitAsync(cancellationToken); // the parts step 2 may have created
                return now is null
                    ? new ShipmentMoveResult(ShipmentMoveOutcome.NoSuchPart, OrderStatus: orderStatus)
                    : new ShipmentMoveResult(
                        now.Status == to && (trackingReference is null || now.TrackingReference == trackingReference)
                            ? ShipmentMoveOutcome.AlreadyThere
                            : ShipmentMoveOutcome.WrongState,
                        now.Status, now.TrackingReference, orderStatus);
            }

            // 4. The order's summary, in words an older image still parses (research D4). Computed here,
            //    under the lock, from every part as it now stands.
            var parts = await _context.OrderShipments
                .AsNoTracking()
                .Where(s => s.OrderId == orderId)
                .Select(s => new { s.Status, s.TrackingReference })
                .ToListAsync(cancellationToken);

            var summary = parts.All(p => p.Status == ShipmentStatus.Shipped)
                ? OrderStatus.Shipped
                : parts.Any(p => p.Status != ShipmentStatus.Pending)
                    ? OrderStatus.Preparing
                    : orderStatus.Value;

            // One parcel: the order's tracking IS the part's, so an old reader still shows it. Several:
            // none of them is "the order's", and the parts carry their own.
            var summaryTracking = parts.Count == 1 ? parts[0].TrackingReference : null;

            await _context.Orders
                .Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(o => o.Status, summary)
                          .SetProperty(o => o.TrackingReference, summaryTracking)
                          .SetProperty(o => o.UpdatedAt, at),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ShipmentMoveResult(ShipmentMoveOutcome.Moved, now!.Status, now.TrackingReference, summary);
        });
    }

    public async Task<Domain.Entities.Order?> GetByIdForUserAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Both predicates, one query. Loading by id and comparing the owner afterwards would read
        // another shopper's order into memory before deciding to refuse it, and would make a "not
        // yours" answer distinguishable from a "no such order" one.
        return await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Shipments)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
    }
}
