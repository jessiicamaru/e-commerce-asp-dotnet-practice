using FluentValidation;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

public class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator()
    {
        RuleFor(x => x.TrackingReference)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("A tracking reference of at most 100 characters is required to mark an order sent.");
    }
}
