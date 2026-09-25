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
            .Include(x => x.Shipments).ThenInclude(s => s.Return)
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
        CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? stage = null)
    {
        if (stage is null)
        {
            return await SettleAsync(orderId, settledStatus, failureReason, settledAt, cancellationToken);
        }

        // Called from a consumer, this runs inside MassTransit's consumer outbox, which already holds a
        // transaction on this context and commits it - with the staged messages - after the consumer.
        // Join it: a second BeginTransaction throws, and clearing the tracker would drop the inbox row.
        if (_context.Database.CurrentTransaction is not null)
        {
            var joined = await SettleAsync(orderId, settledStatus, failureReason, settledAt, cancellationToken);
            if (joined > 0)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return joined;
        }

        // The settlement and what announces it, in one transaction (specs/041, 042).
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var settled = await SettleAsync(orderId, settledStatus, failureReason, settledAt, cancellationToken);

            if (settled > 0)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return settled;
        });
    }

    private async Task<int> SettleAsync(
        Guid orderId, OrderStatus settledStatus, string? failureReason, DateTime settledAt, CancellationToken cancellationToken)
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
                // What it earns them (specs/037), as recorded at checkout - or nulls, never recomputed.
                Terms = x.Shipments
                    .Where(s => s.SellerId == sellerId)
                    .Select(s => new { s.GoodsTotal, s.Commission, s.ShippingShare, PaidOut = s.PayoutId != null })
                    .FirstOrDefault(),
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
            // Cancelled (specs/039) says so, whatever state its part was left in.
            x.Status == OrderStatus.Cancelled
                ? OrderMapping.Describe(x.Status)
                : x.Part is { } part ? OrderMapping.Describe(part) : OrderMapping.Describe(x.Status),
            x.CreatedAt,
            x.UpdatedAt,
            x.LineCount,
            x.Units,
            x.Subtotal,
            x.Currency ?? string.Empty,
            x.Terms?.GoodsTotal,
            x.Terms?.Commission,
            x.Terms?.ShippingShare,
            Owed(x.Terms?.GoodsTotal, x.Terms?.Commission, x.Terms?.ShippingShare),
            x.Terms?.PaidOut ?? false)).ToList();

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
                    .Select(s => new
                    {
                        s.Status,
                        s.TrackingReference,
                        s.GoodsTotal,
                        s.Commission,
                        s.ShippingShare,
                        PaidOut = s.PayoutId != null,
                        s.DeliveredAt,
                        s.Return
                    })
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

        // A cancelled sale (specs/039): nothing to ship, so no status of a parcel and no address.
        var cancelled = row.Status == OrderStatus.Cancelled;

        return new SaleDetailResponse(
            row.Id,
            cancelled ? OrderMapping.Describe(row.Status) : OrderMapping.Describe(partStatus),
            row.CreatedAt,
            row.UpdatedAt,
            row.Items,
            row.Items.Sum(i => i.TotalPrice),
            row.Currency ?? string.Empty,
            row.Language ?? string.Empty,
            row.Part?.TrackingReference,
            // ⚠️ Where to send it - only while sending it is their job (specs/035 research D6). Once
            // their parcel is out, a seller holding the customer's home address has no use for it.
            cancelled || partStatus == ShipmentStatus.Shipped ? null : OrderMapping.ToResponse(row.ShipTo),
            row.Part?.GoodsTotal,
            row.Part?.Commission,
            row.Part?.ShippingShare,
            Owed(row.Part?.GoodsTotal, row.Part?.Commission, row.Part?.ShippingShare),
            row.Part?.PaidOut ?? false,
            row.Part?.DeliveredAt,
            row.Part?.Return is { } r ? Ecommerce.Order.Application.Returns.ReturnResponse.From(r) : null);
    }

    /// <summary>What the shop owes for a part, or null when its terms were never recorded (specs/037).</summary>
    private static decimal? Owed(decimal? goods, decimal? commission, decimal? share) =>
        goods is { } g && commission is { } c && share is { } d ? new PartTerms(g, c, d).Payout : null;

    /// <summary>Order statuses in which a part may be worked on: the order has been paid.</summary>
    private static readonly OrderStatus[] Payable =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped];

    public async Task EnsureShipmentsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // The same rule as the AddOrderShipments backfill: one part per seller on the order, and one for
        // the shop's goods, starting in the state the ORDER is in - so an order an older image shipped is
        // a shipped part here, not a part waiting to be shipped a second time.
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO order_shipments ("Id", "OrderId", "SellerId", "Status", "TrackingReference", "UpdatedAt", "ShippedAt")
            SELECT gen_random_uuid(),
                   o."Id",
                   i."SellerId",
                   CASE o."Status" WHEN 'Preparing' THEN 'Preparing' WHEN 'Shipped' THEN 'Shipped' ELSE 'Pending' END,
                   CASE WHEN o."Status" = 'Shipped' THEN o."TrackingReference" END,
                   o."UpdatedAt",
                   CASE WHEN o."Status" = 'Shipped' THEN o."UpdatedAt" END
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
        CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? stage = null)
    {
        // Through the execution strategy, because production enables EnableRetryOnFailure and that
        // refuses a transaction the caller opened itself. The whole unit is retried, which is safe:
        // every write in it is guarded or ON CONFLICT DO NOTHING.
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            // A retried attempt must not save an audit entry the failed attempt staged.
            _context.ChangeTracker.Clear();
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

            if (orderStatus == OrderStatus.Cancelled)
            {
                // Only someone with a part on it learns it was cancelled; anyone else is told there is no
                // such part, in the same words as always (specs/039 research D5).
                var hasPart = await _context.OrderShipments
                    .AnyAsync(s => s.OrderId == orderId && s.SellerId == sellerId, cancellationToken);
                return new ShipmentMoveResult(
                    hasPart ? ShipmentMoveOutcome.OrderCancelled : ShipmentMoveOutcome.NoSuchPart,
                    OrderStatus: orderStatus);
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

            // Shipping records WHEN, which automatic delivery counts from (specs/040 research D2).
            var moved = trackingReference is null
                ? await part.ExecuteUpdateAsync(
                    x => x.SetProperty(s => s.Status, to).SetProperty(s => s.UpdatedAt, at), cancellationToken)
                : await part.ExecuteUpdateAsync(
                    x => x.SetProperty(s => s.Status, to)
                          .SetProperty(s => s.TrackingReference, trackingReference)
                          .SetProperty(s => s.ShippedAt, to == ShipmentStatus.Shipped ? at : (DateTime?)null)
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

            // 5. What goes with the move - its audit entry - in the same transaction (specs/041).
            if (stage is not null)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new ShipmentMoveResult(ShipmentMoveOutcome.Moved, now!.Status, now.TrackingReference, summary);
        });
    }

    public Task<DeliveryConfirmOutcome> TryConfirmDeliveryAsync(
        Guid orderId, Guid shipmentId, Guid ownerId, DateTime at, CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? stage = null)
    {
        // One transaction: the guarded UPDATE and, when it changed the row, its audit entry (specs/041).
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var outcome = await ConfirmDeliveryAsync(orderId, shipmentId, ownerId, at, cancellationToken);

            if (outcome == DeliveryConfirmOutcome.Confirmed && stage is not null)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return outcome;
        });
    }

    private async Task<DeliveryConfirmOutcome> ConfirmDeliveryAsync(
        Guid orderId, Guid shipmentId, Guid ownerId, DateTime at, CancellationToken cancellationToken)
    {
        // The parcel, of this order, of this owner - all in the query, so a stranger's parcel is never read
        // and then refused; it is simply not there (research D3).
        var mine = _context.OrderShipments.Where(s =>
            s.Id == shipmentId && s.OrderId == orderId && s.Order!.UserId == ownerId);

        // One guarded statement: shipped, and not delivered yet. A repeat, or a sweep that got there first,
        // affects no row - and the first word stands.
        var confirmed = await mine
            .Where(s => s.Status == ShipmentStatus.Shipped && s.DeliveredAt == null)
            .ExecuteUpdateAsync(
                x => x.SetProperty(s => s.DeliveredAt, at)
                      .SetProperty(s => s.DeliveryConfirmedBy, ParcelDelivery.ByCustomer),
                cancellationToken);

        if (confirmed == 1)
        {
            return DeliveryConfirmOutcome.Confirmed;
        }

        // Nothing changed: say why, from what is there now.
        var now = await mine
            .AsNoTracking()
            .Select(s => new { s.Status, s.DeliveredAt })
            .FirstOrDefaultAsync(cancellationToken);

        return now is null
            ? DeliveryConfirmOutcome.NotFound
            : now.DeliveredAt is not null
                ? DeliveryConfirmOutcome.AlreadyDelivered
                : DeliveryConfirmOutcome.NotShipped;
    }

    public Task<int> AutoConfirmDeliveriesAsync(
        DateTime shippedBefore, DateTime at, CancellationToken cancellationToken = default,
        Func<IReadOnlyList<Guid>, CancellationToken, Task>? stage = null)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var confirmed = await SweepDeliveriesAsync(shippedBefore, at, cancellationToken);

            if (confirmed.Count > 0 && stage is not null)
            {
                await stage(confirmed, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return confirmed.Count;
        });
    }

    private async Task<List<Guid>> SweepDeliveriesAsync(DateTime shippedBefore, DateTime at, CancellationToken cancellationToken)
    {
        // The same guard as the customer's, plus the window. Two instances sweeping at once, or a customer
        // confirming mid-sweep: each row is set once, by whoever is first (research D3).
        //
        // The rows are LOCKED first, and the ones this sweep then sets are the ones it tells about
        // (specs/046): a row a customer confirmed in the meantime fails the guard below, is not in the
        // list, and is announced once - by the customer's confirmation.
        var due = await _context.OrderShipments
            .FromSqlInterpolated($"""
                SELECT * FROM order_shipments
                WHERE "Status" = 'Shipped' AND "DeliveredAt" IS NULL AND "ShippedAt" IS NOT NULL AND "ShippedAt" <= {shippedBefore}
                FOR UPDATE SKIP LOCKED
                """)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return due;
        }

        await _context.OrderShipments
            .Where(s => due.Contains(s.Id) && s.Status == ShipmentStatus.Shipped && s.DeliveredAt == null)
            .ExecuteUpdateAsync(
                x => x.SetProperty(s => s.DeliveredAt, at)
                      .SetProperty(s => s.DeliveryConfirmedBy, ParcelDelivery.ByAuto),
                cancellationToken);

        return due;
    }

    public async Task<List<DeliveredParcel>> GetDeliveredParcelsAsync(
        IEnumerable<Guid> shipmentIds, CancellationToken cancellationToken = default)
    {
        var ids = shipmentIds.ToList();
        var rows = await (
            from s in _context.OrderShipments.AsNoTracking()
            where ids.Contains(s.Id) && s.DeliveredAt != null
            join o in _context.Orders.AsNoTracking() on s.OrderId equals o.Id
            from i in o.Items
            where i.SellerId == s.SellerId
            select new { s.OrderId, ShipmentId = s.Id, o.UserId, i.ProductId, DeliveredAt = s.DeliveredAt!.Value })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.OrderId, r.ShipmentId, r.UserId, r.DeliveredAt))
            .Select(g => new DeliveredParcel(g.Key.OrderId, g.Key.ShipmentId, g.Key.UserId,
                g.Select(r => r.ProductId).Distinct().ToList(), g.Key.DeliveredAt))
            .ToList();
    }

    public Task<CancelOutcome> TryCancelAsync(
        Guid orderId,
        Guid? ownerId,
        bool allowWhilePreparing,
        string cancelledBy,
        DateTime at,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default)
    {
        // The execution strategy, for the same reason as TryMoveShipmentAsync. A retried attempt starts
        // from a clean change tracker, so an outbox message staged by a failed attempt is not saved twice.
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // 1. The order's row lock - the SAME one every parcel move takes first, which is what makes a
            //    cancel and a ship on one order run one after the other (research D2).
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM orders WHERE "Id" = {orderId} FOR UPDATE""", cancellationToken);

            // The owner is part of the query, so someone else's order is simply not found.
            var status = await _context.Orders
                .Where(o => o.Id == orderId && (ownerId == null || o.UserId == ownerId))
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (status is null)
            {
                return CancelOutcome.NotFound;
            }

            if (status == OrderStatus.Cancelled)
            {
                return CancelOutcome.AlreadyCancelled;
            }

            if (!Payable.Contains(status.Value))
            {
                return CancelOutcome.NotPaid;
            }

            // 2. Parts an older image never wrote, in the order's state - so a legacy "Preparing" order is
            //    seen as being prepared, not as waiting.
            await EnsureShipmentsAsync(orderId, cancellationToken);

            var parts = await _context.OrderShipments
                .AsNoTracking()
                .Where(s => s.OrderId == orderId)
                .Select(s => s.Status)
                .ToListAsync(cancellationToken);

            if (parts.Any(p => p == ShipmentStatus.Shipped))
            {
                await transaction.CommitAsync(cancellationToken); // the parts step 2 may have created
                return CancelOutcome.Shipped;
            }

            if (!allowWhilePreparing && parts.Any(p => p != ShipmentStatus.Pending))
            {
                await transaction.CommitAsync(cancellationToken);
                return CancelOutcome.BeingPrepared;
            }

            // 3. The guarded statement, then the event, then one save: the row and the outbox message
            //    commit together.
            var cancelled = await _context.Orders
                .Where(o => o.Id == orderId && Payable.Contains(o.Status))
                .ExecuteUpdateAsync(
                    x => x.SetProperty(o => o.Status, OrderStatus.Cancelled)
                          .SetProperty(o => o.CancelledBy, cancelledBy)
                          .SetProperty(o => o.UpdatedAt, at),
                    cancellationToken);

            if (cancelled == 0)
            {
                return CancelOutcome.AlreadyCancelled; // unreachable under the lock; guarded all the same
            }

            await stage(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CancelOutcome.Cancelled;
        });
    }

    public async Task<OrderNoticeFacts?> GetNoticeFactsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await EnsureShipmentsAsync(orderId, cancellationToken);

        var order = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.UserId,
                o.TotalAmount,
                o.Currency,
                o.Language,
                Parts = o.Shipments.Select(s => new { s.Id, s.SellerId }).ToList(),
                Names = o.Items.Where(i => i.SellerName != null).Select(i => new { i.SellerId, i.SellerName }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        return new OrderNoticeFacts(
            orderId,
            order.UserId,
            order.TotalAmount,
            order.Currency ?? string.Empty,
            order.Parts.Select(p => new ParcelFact(
                p.Id, p.SellerId, order.Names.FirstOrDefault(n => n.SellerId == p.SellerId)?.SellerName)).ToList(),
            order.Language ?? string.Empty);
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
            .Include(x => x.Shipments).ThenInclude(s => s.Return)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
    }
}
