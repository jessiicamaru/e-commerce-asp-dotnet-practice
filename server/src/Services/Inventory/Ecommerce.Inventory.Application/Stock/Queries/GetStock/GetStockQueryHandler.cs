using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Common.Models;
using Ecommerce.Inventory.Application.Stock.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Queries.GetStock;

public class GetStockQueryHandler(IStockRepository stockRepository)
    : IRequestHandler<GetStockQuery, PaginatedList<StockResponse>>
{
    private readonly IStockRepository _stockRepository = stockRepository;

    public async Task<PaginatedList<StockResponse>> Handle(GetStockQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await _stockRepository
            .GetPaginatedAsync(pageNumber, pageSize, request.Sku, cancellationToken);

        var responses = items
            .Select(x => new StockResponse(
                x.ProductId, x.Sku, x.QuantityOnHand, x.QuantityReserved, x.QuantityAvailable))
            .ToList();

        return new PaginatedList<StockResponse>(responses, totalCount, pageNumber, pageSize);
    }
}
