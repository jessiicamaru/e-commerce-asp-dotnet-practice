using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Stock.Common;
using Ecommerce.Shared.Exceptions;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;

public class SetStockOnHandCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IPublishEndpoint publishEndpoint
) : IRequestHandler<SetStockOnHandCommand, StockResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<StockResponse> Handle(SetStockOnHandCommand request, CancellationToken cancellationToken)
    {
        StockResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Same lock as the reserve path, so an adjustment cannot interleave with a reservation
            // and leave units promised against stock that no longer exists.
            var items = await _stockRepository.GetForUpdateAsync([request.ProductId], ct);

            var stock = items.FirstOrDefault()
                ?? throw new NotFoundException(
                    $"Product '{request.ProductId}' is not registered in inventory.");

            if (request.QuantityOnHand < stock.QuantityReserved)
            {
                throw new ConflictException(
                    $"Cannot set quantity on hand to {request.QuantityOnHand}: "
                    + $"{stock.QuantityReserved} unit(s) are currently reserved for orders.");
            }

            stock.QuantityOnHand = request.QuantityOnHand;
            stock.UpdatedAt = DateTime.UtcNow;

            await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, stock, ct);

            await _stockRepository.SaveChangesAsync(ct);

            response = new StockResponse(
                stock.ProductId,
                stock.Sku,
                stock.QuantityOnHand,
                stock.QuantityReserved,
                stock.QuantityAvailable);
        }, cancellationToken);

        return response!;
    }
}
