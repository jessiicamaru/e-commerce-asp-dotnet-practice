using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.RemoveLine;

/// <param name="ProductId">
/// The sellable unit to remove: a VARIANT id since specs/020. For a line added before variants, and
/// for a product sold in one shape, that is the product's own id, so an older client still works.
/// </param>
public record RemoveLineCommand(Guid ProductId) : IRequest;
