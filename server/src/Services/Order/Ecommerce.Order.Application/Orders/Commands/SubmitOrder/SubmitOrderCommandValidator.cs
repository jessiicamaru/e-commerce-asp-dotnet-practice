using FluentValidation;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

/// <remarks>
/// Nothing left to validate on the request itself: it carries no items, no user and no price. An
/// empty cart is refused by the handler, because only the handler has read the cart.
/// </remarks>
public class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
{
}
