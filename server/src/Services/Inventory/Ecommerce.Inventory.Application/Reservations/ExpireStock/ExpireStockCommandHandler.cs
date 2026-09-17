using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Reservations.ExpireStock;

/// <summary>Reclaims stock held by orders that never settled, so none is stranded.</summary>
public class ExpireStockCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<ExpireStockCommandHandler> logger
) : IRequestHandler<ExpireStockCommand, int>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IReservationRepository _reservationRepository = reservationRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<ExpireStockCommandHandler> _logger = logger;

    public async Task<int> Handle(ExpireStockCommand request, CancellationToken cancellationToken)
    {
        var settled = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var reservations = await _reservationRepository
                .GetExpiredAsync(DateTime.UtcNow, request.BatchSize, ct);

            if (reservations.Count == 0)
            {

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

                reservation.Status = ReservationStatus.Expired;
                reservation.SettledAt = DateTime.UtcNow;
                reservation.SettlementReason = "Holding period elapsed with no settlement";
                settled++;
            }

            // Every stock row this loop touched, announced before the single save so the
            // movement and the announcement commit together.
            await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, stockItems, ct);

            await _reservationRepository.SaveChangesAsync(ct);
        }, cancellationToken);

        if (settled > 0)
        {
            _logger.LogWarning(
                "Reclaimed {Count} reservation(s) whose holding period had elapsed.", settled);
        }


        return settled;
    }
}
