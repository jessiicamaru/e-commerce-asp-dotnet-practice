using FluentValidation;

namespace Ecommerce.Cart.Application.Carts.Commands.SetQuantity;

public class SetQuantityCommandValidator : AbstractValidator<SetQuantityCommand>
{
    public SetQuantityCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("Quantity cannot be negative.");
    }
}
