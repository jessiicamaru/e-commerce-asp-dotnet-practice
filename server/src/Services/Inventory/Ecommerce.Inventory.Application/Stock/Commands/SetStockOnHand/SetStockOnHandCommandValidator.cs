using FluentValidation;

namespace Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;

public class SetStockOnHandCommandValidator : AbstractValidator<SetStockOnHandCommand>
{
    public SetStockOnHandCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("ProductId is required.");

        RuleFor(x => x.QuantityOnHand)
            .GreaterThanOrEqualTo(0).WithMessage("QuantityOnHand cannot be negative.");
    }
}
