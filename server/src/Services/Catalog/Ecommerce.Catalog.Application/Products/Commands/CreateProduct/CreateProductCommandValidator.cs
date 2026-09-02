using FluentValidation;

namespace Ecommerce.Catalog.Application.Products.Commands.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must be less than 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Product description must be less than 2000 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Product price must be greater than 0.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Product stock quantity must be greater than or equal to 0.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Product SKU is required.")
            .MaximumLength(50).WithMessage("Product SKU must be less than 50 characters.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Product category ID is required.");
    }
}
