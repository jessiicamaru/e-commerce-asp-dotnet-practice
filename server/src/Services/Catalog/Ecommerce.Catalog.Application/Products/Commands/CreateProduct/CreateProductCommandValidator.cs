using FluentValidation;

namespace Ecommerce.Catalog.Application.Products.Commands.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        // TODO: Write validation rules for CreateProductCommand:
        // - Name: NotEmpty(), MaximumLength(200)
        // - Description: MaximumLength(2000) (if present)
        // - Price: GreaterThan(0)
        // - StockQuantity: GreaterThanOrEqualTo(0)
        // - Sku: NotEmpty(), MaximumLength(50)
        // - CategoryId: NotEmpty()
    }
}
