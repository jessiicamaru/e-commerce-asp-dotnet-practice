using FluentValidation;

namespace Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must be less than 100 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Category slug is required.")
            .MaximumLength(150).WithMessage("Category slug must be less than 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Category description must be less than 500 characters.");
    }
}
