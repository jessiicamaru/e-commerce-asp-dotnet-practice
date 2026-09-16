using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Reservations.ReleaseStock;

/// <summary>Returns held units to available. Publishes nothing: the saga awaits no reply.</summary>
public class ReleaseStockCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    ILogger<ReleaseStockCommandHandler> logger
) : IRequestHandler<ReleaseStockCommand, int>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IReservationRepository _reservationRepository = reservationRepository;
    private readonly ILogger<ReleaseStockCommandHandler> _logger = logger;

    public async Task<int> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        var settled = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Only rows still Held. An order never reserved, or already released, confirmed or
            // expired, yields nothing here — which is what makes a repeat a no-op rather than a
            // second movement of stock.
            var reservations = await _reservationRepository
                .GetByOrderIdAndStatusAsync(request.OrderId, ReservationStatus.Held, ct);

            if (reservations.Count == 0)
            {
                _logger.LogInformation(
                    "No held reservations for order {OrderId}; nothing to release.", request.OrderId);
                return;
            }

            // Same lock, same ascending ProductId order as the reserve path. A different order
            // here would turn this into a deadlock source against live reservations.
            var stockItems = await _stockRepository.GetForUpdateAsync(
                reservations.Select(x => x.ProductId), ct);

            var byProduct = stockItems.ToDictionary(x => x.ProductId);

            foreach (var reservation in reservations)
            {
                if (byProduct.TryGetValue(reservation.ProductId, out var stock))
                {
                    stock.QuantityReserved -= reservation.Quantity;
                    stock.UpdatedAt = DateTime.UtcNow;
                }

                reservation.Status = ReservationStatus.Released;
                reservation.SettledAt = DateTime.UtcNow;
                reservation.SettlementReason = request.Reason;
                settled++;
            }

            await _reservationRepository.SaveChangesAsync(ct);
        }, cancellationToken);


        return settled;
    }
}
