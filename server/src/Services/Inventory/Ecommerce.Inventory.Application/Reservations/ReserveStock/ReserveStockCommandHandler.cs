using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Reservations.ReserveStock;

public class ReserveStockCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    IPublishEndpoint publishEndpoint,
    IConfiguration configuration,
    ILogger<ReserveStockCommandHandler> logger
) : IRequestHandler<ReserveStockCommand, ReserveStockResult>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IReservationRepository _reservationRepository = reservationRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<ReserveStockCommandHandler> _logger = logger;

    private const int DefaultHoldingPeriodMinutes = 15;

    public async Task<ReserveStockResult> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var result = ReserveStockResult.Success();

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Rows whose availability this delivery actually moved. Declared out here because the
            // locked items live inside the else-branch below, and only a successful reservation
            // changes anything worth announcing - a failed one leaves every row as it found it.
            List<StockItem> movedStock = [];

            // The broker redelivers as normal operation. Reservations already on file for this
            // order mean the original delivery succeeded and its reply is already in the outbox;
            // doing the work again would deduct stock twice.
            if (await _reservationRepository.ExistsForOrderAsync(request.OrderId, ct))
            {
                _logger.LogInformation(
                    "Order {OrderId} already holds reservations; treating this delivery as a duplicate.",
                    request.OrderId);
                return;
            }

            // The same product may appear on several lines. Evaluating them separately would let an
            // order for 3 and then 4 units pass against a stock of 5.
            var requested = request.Items
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            var invalidLine = requested.FirstOrDefault(x => x.Quantity <= 0);
            if (invalidLine is not null)
            {
                result = ReserveStockResult.Failure(
                    $"Invalid quantity {invalidLine.Quantity} for product {invalidLine.ProductId}");
            }
            else if (requested.Count == 0)
            {
                result = ReserveStockResult.Failure("Order contains no items");
            }
            else
            {
                // Locks held until this transaction commits, in ascending ProductId order.
                var stockItems = await _stockRepository.GetForUpdateAsync(
                    requested.Select(x => x.ProductId), ct);

                var byProduct = stockItems.ToDictionary(x => x.ProductId);

                foreach (var line in requested)
                {
                    if (!byProduct.TryGetValue(line.ProductId, out var stock))
                    {
                        result = ReserveStockResult.Failure($"Unknown product {line.ProductId}");
                        break;
                    }

                    if (stock.QuantityAvailable < line.Quantity)
                    {
                        result = ReserveStockResult.Failure(
                            $"Insufficient stock for product {line.ProductId}: "
                            + $"requested {line.Quantity}, available {stock.QuantityAvailable}");
                        break;
                    }
                }

                if (result.Succeeded)
                {
                    // All-or-nothing is the transaction boundary, not a loop that undoes itself.
                    var expiresAt = DateTime.UtcNow.AddMinutes(HoldingPeriodMinutes());
                    var reservations = new List<StockReservation>();

                    foreach (var line in requested)
                    {
                        var stock = byProduct[line.ProductId];
                        stock.QuantityReserved += line.Quantity;
                        stock.UpdatedAt = DateTime.UtcNow;

                        reservations.Add(new StockReservation
                        {
                            Id = Guid.CreateVersion7(),
                            OrderId = request.OrderId,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            Status = ReservationStatus.Held,
                            ExpiresAt = expiresAt,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    await _reservationRepository.AddRangeAsync(reservations, ct);

                    movedStock = requested.Select(line => byProduct[line.ProductId]).ToList();
                }
            }

            // Publish BEFORE the single SaveChangesAsync, so the stock rows, the reservation rows
            // and the outbox entry all commit together. Publishing after the save would allow a
            // reply describing a change the database never accepted, or the reverse.
            if (result.Succeeded)
            {
                await _publishEndpoint.Publish(
                    new InventoryReservedEvent(request.OrderId, DateTime.UtcNow), ct);

                // Reserving the last units is the commonest way a product becomes unbuyable, so
                // this is the announcement the catalogue most depends on.
                await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, movedStock, ct);
            }
            else
            {
                await _publishEndpoint.Publish(
                    new InventoryReservationFailedEvent(request.OrderId, result.FailureReason!), ct);
            }

            await _stockRepository.SaveChangesAsync(ct);
        }, cancellationToken);

        return result;
    }

    private int HoldingPeriodMinutes()
    {
        var configured = _configuration["Inventory:ReservationTtlMinutes"];

        return int.TryParse(configured, out var minutes) && minutes > 0
            ? minutes
            : DefaultHoldingPeriodMinutes;
    }
}
