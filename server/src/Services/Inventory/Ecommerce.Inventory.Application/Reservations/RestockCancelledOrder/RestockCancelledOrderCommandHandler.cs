using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;

/// <summary>
/// A paid order was cancelled before anything shipped (specs/039): whatever it took goes back.
/// </summary>
/// <remarks>
/// <para>
/// Two cases, because <c>OrderCompletedEvent</c> and <c>OrderCancelledEvent</c> reach this service in
/// either order (research D4):
/// </para>
/// <list type="bullet">
/// <item><b>Confirmed</b> - the units left the shelf: <c>QuantityOnHand</c> rises by what was taken.</item>
/// <item><b>Held</b> - completion has not arrived yet: the hold is freed, and the late completion then
/// finds no Held row and deducts nothing.</item>
/// </list>
/// <para>
/// ⚠️ Both end as <see cref="ReservationStatus.Released"/>, with the reason saying why - deliberately NOT a
/// new status, which an image rolled back to before this could not parse (research D3). The guard is the
/// status each row moves FROM, so a redelivery finds nothing and moves nothing.
/// </para>
/// </remarks>
public class RestockCancelledOrderCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<RestockCancelledOrderCommandHandler> logger
) : IRequestHandler<RestockCancelledOrderCommand, int>
{
    public const string Reason = "Returned: order cancelled";

    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IReservationRepository _reservationRepository = reservationRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<RestockCancelledOrderCommandHandler> _logger = logger;

    public async Task<int> Handle(RestockCancelledOrderCommand request, CancellationToken cancellationToken)
    {
        var settled = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var confirmed = await _reservationRepository
                .GetByOrderIdAndStatusAsync(request.OrderId, ReservationStatus.Confirmed, ct);
            var held = await _reservationRepository
                .GetByOrderIdAndStatusAsync(request.OrderId, ReservationStatus.Held, ct);

            if (confirmed.Count == 0 && held.Count == 0)
            {
                _logger.LogInformation(
                    "Nothing held or taken for cancelled order {OrderId}; nothing to put back.", request.OrderId);
                return;
            }

            // The same lock, in the same ascending order, as every other path that moves stock.
            var stockItems = await _stockRepository.GetForUpdateAsync(
                confirmed.Concat(held).Select(x => x.ProductId), ct);
            var byProduct = stockItems.ToDictionary(x => x.ProductId);
            var now = DateTime.UtcNow;

            foreach (var reservation in confirmed)
            {
                if (byProduct.TryGetValue(reservation.ProductId, out var stock))
                {
                    stock.QuantityOnHand += reservation.Quantity;
                    stock.UpdatedAt = now;
                }

                Settle(reservation, now);
            }

            foreach (var reservation in held)
            {
                if (byProduct.TryGetValue(reservation.ProductId, out var stock))
                {
                    stock.QuantityReserved -= reservation.Quantity;
                    stock.UpdatedAt = now;
                }

                Settle(reservation, now);
            }

            settled = confirmed.Count + held.Count;

            // The seventh path that moves stock, and it announces like the other six - or Catalog keeps
            // showing "out of stock" for units that are back on the shelf.
            await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, stockItems, ct);
            await _reservationRepository.SaveChangesAsync(ct);
        }, cancellationToken);

        return settled;
    }

    private static void Settle(Domain.Entities.StockReservation reservation, DateTime at)
    {
        reservation.Status = ReservationStatus.Released;
        reservation.SettledAt = at;
        reservation.SettlementReason = Reason;
    }
}
