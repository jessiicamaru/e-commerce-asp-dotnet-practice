using Ecommerce.Inventory.Application.Common.Models;
using Ecommerce.Inventory.Application.Stock.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Queries.GetStock;

public record GetStockQuery(
    int PageNumber = 1,
    int PageSize = 12,
    string? Sku = null
) : IRequest<PaginatedList<StockResponse>>;
