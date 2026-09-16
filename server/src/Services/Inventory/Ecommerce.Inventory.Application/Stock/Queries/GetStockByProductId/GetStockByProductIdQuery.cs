using Ecommerce.Inventory.Application.Stock.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Queries.GetStockByProductId;

public record GetStockByProductIdQuery(Guid ProductId) : IRequest<StockResponse>;
