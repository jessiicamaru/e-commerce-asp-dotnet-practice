using Ecommerce.Order.Application.Returns;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

public class ReturnRepository(OrderDbContext context) : IReturnRepository
{
    private readonly OrderDbContext _context = context;

    public Task<ReturnParcel?> GetParcelAsync(Guid orderId, Guid shipmentId, Guid? buyerId, CancellationToken cancellationToken = default) =>
        // The parcel, of this order, of this buyer - all in the query, so a stranger's is simply not there.
        ParcelsAsync(_context.OrderShipments.Where(s =>
            s.Id == shipmentId && s.OrderId == orderId && (buyerId == null || s.Order!.UserId == buyerId)), cancellationToken);

    public Task<ReturnParcel?> GetSellerParcelAsync(Guid orderId, Guid sellerId, CancellationToken cancellationToken = default) =>
        ParcelsAsync(_context.OrderShipments.Where(s => s.OrderId == orderId && s.SellerId == sellerId), cancellationToken);

    private async Task<ReturnParcel?> ParcelsAsync(IQueryable<OrderShipment> query, CancellationToken cancellationToken)
    {
        var row = await query
            .AsNoTracking()
            .Select(s => new
            {
                s.OrderId,
                ShipmentId = s.Id,
                BuyerId = s.Order!.UserId,
                s.SellerId,
                OrderStatus = s.Order.Status,
                s.Status,
                s.DeliveredAt,
                s.Order.Currency,
                // The part's lines: an order's lines belong to the part of their seller (specs/035).
                Lines = s.Order.Items
                    .Where(i => i.SellerId == s.SellerId)
                    .Select(i => new ReturnParcelLine(i.VariantId ?? i.ProductId, i.Quantity, i.UnitPrice, i.TaxAmount, i.ShopDiscount + i.PlatformDiscount))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new ReturnParcel(row.OrderId, row.ShipmentId, row.BuyerId, row.SellerId, row.OrderStatus, row.Status,
                row.DeliveredAt, row.Currency ?? string.Empty, row.Lines);
    }

    public Task<ParcelReturn?> GetForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
        _context.ParcelReturns.AsNoTracking().FirstOrDefaultAsync(r => r.ShipmentId == shipmentId, cancellationToken);

    public Task<bool> TryRequestAsync(ParcelReturn request, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Once per parcel: of two requests at once, the unique index lets one row in and the other inserts nothing.
            var inserted = await _context.Database.ExecuteSqlAsync($"""
                INSERT INTO parcel_returns ("Id", "OrderId", "ShipmentId", "CustomerId", "SellerId", "Status", "Reason",
                                            "RequestedAt", "UpdatedAt")
                VALUES ({request.Id}, {request.OrderId}, {request.ShipmentId}, {request.CustomerId}, {request.SellerId},
                        {request.Status.ToString()}, {request.Reason}, {request.RequestedAt}, {request.UpdatedAt})
                ON CONFLICT ("ShipmentId") DO NOTHING
                """, cancellationToken);

            if (inserted == 1)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return inserted == 1;
        });
    }

    public Task<bool> TryMoveAsync(ReturnMove move, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var from = move.From;
            var decided = move.To is ReturnStatus.Accepted or ReturnStatus.Refused or ReturnStatus.Rejected;

            // ONE guarded statement: from the state this step expects - and, for a step the buyer takes after a
            // decision, still inside that decision's window. A second or late step changes no row.
            var moved = await _context.ParcelReturns
                .Where(r => r.Id == move.ReturnId && from.Contains(r.Status)
                    && (move.DecidedAfter == null || r.DecidedAt > move.DecidedAfter))
                .ExecuteUpdateAsync(x => x
                    .SetProperty(r => r.Status, move.To)
                    .SetProperty(r => r.UpdatedAt, move.At)
                    .SetProperty(r => r.DecidedAt, r => decided ? move.At : r.DecidedAt)
                    .SetProperty(r => r.DecisionReason, r => decided ? move.DecisionReason : r.DecisionReason)
                    .SetProperty(r => r.TrackingReference, r => move.TrackingReference ?? r.TrackingReference)
                    .SetProperty(r => r.SentBackAt, r => move.To == ReturnStatus.SentBack ? move.At : r.SentBackAt)
                    .SetProperty(r => r.ReceivedAt, r => move.To == ReturnStatus.Received ? move.At : r.ReceivedAt)
                    .SetProperty(r => r.RefundAmount, r => move.RefundAmount ?? r.RefundAmount),
                    cancellationToken);

            if (moved == 1)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return moved == 1;
        });
    }

    public async Task<(List<ReturnResponse> Items, int TotalCount)> GetPageAsync(
        ReturnStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ParcelReturns.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        // A queue: the longest waiting first.
        var rows = await query
            .OrderBy(r => r.UpdatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (rows.Select(ReturnResponse.From).ToList(), total);
    }
}
