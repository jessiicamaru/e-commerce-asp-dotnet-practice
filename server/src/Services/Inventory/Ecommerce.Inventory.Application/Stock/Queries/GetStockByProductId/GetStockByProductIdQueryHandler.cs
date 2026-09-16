using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Stock.Common;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Queries.GetStockByProductId;

public class GetStockByProductIdQueryHandler(IStockRepository stockRepository)
    : IRequestHandler<GetStockByProductIdQuery, StockResponse>
{
    private readonly IStockRepository _stockRepository = stockRepository;

    public async Task<StockResponse> Handle(GetStockByProductIdQuery request, CancellationToken cancellationToken)
    {
        // Not registered and registered-but-empty are different answers: the first is a 404, the
        // second a legitimate zero. Collapsing them would hide an unregistered product.
        var stock = await _stockRepository.GetByProductIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(
                $"Product '{request.ProductId}' is not registered in inventory.");

        return new StockResponse(
            stock.ProductId,
            stock.Sku,
            stock.QuantityOnHand,
            stock.QuantityReserved,
            stock.QuantityAvailable);
    }
}
