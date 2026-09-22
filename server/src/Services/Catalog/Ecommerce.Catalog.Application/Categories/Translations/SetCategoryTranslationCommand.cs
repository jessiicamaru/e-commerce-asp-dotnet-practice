using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Categories.Translations;

/// <summary>
/// Gives a category its name and description in one language, or replaces them (specs/026).
/// </summary>
/// <remarks>
/// An upsert, like a product's: a translation is a fact about a category in a language rather than an
/// event, so the second attempt at the English name should be the English name.
/// </remarks>
public record SetCategoryTranslationCommand(
    Guid CategoryId,
    string Language,
    string Name,
    string? Description
) : IRequest<CategoryResponse>;

public record RemoveCategoryTranslationCommand(Guid CategoryId, string Language) : IRequest;

public class SetCategoryTranslationCommandValidator : AbstractValidator<SetCategoryTranslationCommand>
{
    public SetCategoryTranslationCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public class RemoveCategoryTranslationCommandValidator : AbstractValidator<RemoveCategoryTranslationCommand>
{
    public RemoveCategoryTranslationCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public class SetCategoryTranslationCommandHandler(
    ICategoryRepository categories,
    IOptions<LanguageOptions> localization)
    : IRequestHandler<SetCategoryTranslationCommand, CategoryResponse>
{
    private readonly ICategoryRepository _categories = categories;
    private readonly LanguageOptions _localization = localization.Value;

    public async Task<CategoryResponse> Handle(SetCategoryTranslationCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException($"Category with ID '{request.CategoryId}' was not found.");

        var language = request.Language.ToLowerInvariant();
        var translation = category.Translations.FirstOrDefault(t => t.Language == language);

        if (translation is null)
        {
            category.Translations.Add(new CategoryTranslation
            {
                Id = Guid.CreateVersion7(),
                CategoryId = category.Id,
                Language = language,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
            });
        }
        else
        {
            translation.Name = request.Name.Trim();
            translation.Description = request.Description?.Trim();
        }

        category.UpdatedAt = DateTime.UtcNow;
        await _categories.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category, language, _localization.DefaultLanguage);
    }
}

public class RemoveCategoryTranslationCommandHandler(ICategoryRepository categories)
    : IRequestHandler<RemoveCategoryTranslationCommand>
{
    private readonly ICategoryRepository _categories = categories;

    public async Task Handle(RemoveCategoryTranslationCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException($"Category with ID '{request.CategoryId}' was not found.");

        var language = request.Language.ToLowerInvariant();
        var translation = category.Translations.FirstOrDefault(t => t.Language == language);

        if (translation is null)
        {
            // Removing what is not there is not an error: the category already shows its default text.
            return;
        }

        category.Translations.Remove(translation);
        await _categories.SaveChangesAsync(cancellationToken);
    }
}
