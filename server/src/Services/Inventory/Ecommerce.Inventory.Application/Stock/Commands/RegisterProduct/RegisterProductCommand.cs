using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;

/// <summary>
/// Registers a product the catalogue has created, at zero units. Inventory owns sellable quantity;
/// the catalogue never sets it.
/// </summary>
public record RegisterProductCommand(Guid ProductId, string Sku) : IRequest<bool>;
