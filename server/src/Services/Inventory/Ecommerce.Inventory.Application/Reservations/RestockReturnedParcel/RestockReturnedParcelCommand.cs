using Ecommerce.Contracts.Order;
using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Reservations.RestockReturnedParcel;

/// <summary>
/// A returned parcel came back to its seller (specs/066): its units go back on the shelf, once. Returns how many.
/// </summary>
public record RestockReturnedParcelCommand(Guid ReturnId, Guid OrderId, List<ReturnedItemDto> Items) : IRequest<int>;

/// <remarks>
/// <para>
/// Unlike a cancellation, this is not decided from reservations: a returned parcel is PART of an order that
/// was confirmed long ago, so the event names what comes back - Order knows which lines the parcel held.
/// </para>
/// <para>
/// Once, by the database: the return is claimed first (<c>returned_parcels</c>, <c>ON CONFLICT DO NOTHING</c>) in
/// the transaction that moves the stock, so a redelivery - or two at once - finds it claimed and moves nothing.
/// </para>
/// </remarks>
public class RestockReturnedParcelCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    IReturnedParcels returnedParcels,
    IPublishEndpoint publishEndpoint,
    IAuditTrail audit,
    ILogger<RestockReturnedParcelCommandHandler> logger) : IRequestHandler<RestockReturnedParcelCommand, int>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IReservationRepository _reservationRepository = reservationRepository;
    private readonly IReturnedParcels _returnedParcels = returnedParcels;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAuditTrail _audit = audit;
    private readonly ILogger<RestockReturnedParcelCommandHandler> _logger = logger;

    public async Task<int> Handle(RestockReturnedParcelCommand request, CancellationToken cancellationToken)
    {
        var restocked = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = DateTime.UtcNow;
            if (!await _returnedParcels.TryClaimAsync(request.ReturnId, request.OrderId, now, ct))
            {
                _logger.LogInformation("Return {ReturnId} was already put back on the shelf; nothing to do.", request.ReturnId);
                return;
            }

            // The same lock, in the same ascending order, as every other path that moves stock.
            var stockItems = await _stockRepository.GetForUpdateAsync(request.Items.Select(i => i.VariantId), ct);
            var byVariant = stockItems.ToDictionary(x => x.ProductId);

            foreach (var item in request.Items.Where(i => i.Quantity > 0))
            {
                if (byVariant.TryGetValue(item.VariantId, out var stock))
                {
                    stock.QuantityOnHand += item.Quantity;
                    stock.UpdatedAt = now;
                    restocked += item.Quantity;
                }
                else
                {
                    // A variant deleted since - nothing to put it on. Recorded, not invented.
                    _logger.LogWarning("Return {ReturnId}: no stock row for {VariantId}; {Quantity} unit(s) not put back.",
                        request.ReturnId, item.VariantId, item.Quantity);
                }
            }

            // The eighth path that moves stock, and it announces like the other seven - or Catalog keeps showing
            // "out of stock" for units that are back on the shelf (AnnouncementTests).
            await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, stockItems, ct);
            await _audit.RecordAsync(
                AuditCategory.Order, "StockReturned", "Order", request.OrderId.ToString(),
                $"Put back {restocked} unit(s) of a returned parcel",
                after: new { request.ReturnId, Returned = restocked },
                cancellationToken: ct);
            await _reservationRepository.SaveChangesAsync(ct);
        }, cancellationToken);

        return restocked;
    }
}
