using Ecommerce.Inventory.Application.Stock.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;

/// <summary>
/// Absolute value, never a delta: two admins adjusting at once would otherwise lose one update.
/// </summary>
public record SetStockOnHandCommand(Guid ProductId, int QuantityOnHand) : IRequest<StockResponse>;
