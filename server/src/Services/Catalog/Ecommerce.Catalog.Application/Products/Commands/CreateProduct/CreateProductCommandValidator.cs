using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Shared.Money;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Commands.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator(IOptions<CurrencyOptions> money)
    {

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must be less than 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Product description must be less than 2000 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Product price must be greater than 0.");

        // The price on these commands is the DEFAULT currency's (specs/022). A price of 9.99 dong is
        // not a price, and nothing downstream would round it away: a subtotal is a unit price times
        // an integer.
        RuleFor(x => x.Price)
            .MustFitTheCurrency(money.Value, _ => money.Value.DefaultCurrency);

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Product SKU is required.")
            .MaximumLength(50).WithMessage("Product SKU must be less than 50 characters.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Product category ID is required.");
    }
}
