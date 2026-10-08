using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Specifications;

public record ProductSpecificationInput(Guid SpecificationId, Guid? OptionId = null, string? Text = null);

/// <summary>
/// A product's specifications, the whole set at once (specs/159 research D4): what is sent replaces what was there, so
/// the seller's form saves as one and the seeder can send it again.
/// </summary>
public record SetProductSpecificationsCommand(Guid ProductId, List<ProductSpecificationInput> Values)
    : IRequest<List<ProductSpecificationResponse>>;

public class SetProductSpecificationsCommandValidator : AbstractValidator<SetProductSpecificationsCommand>
{
    public SetProductSpecificationsCommandValidator()
    {
        RuleFor(x => x.Values).NotNull();
        RuleFor(x => x.Values).Must(v => v is null || v.Select(i => i.SpecificationId).Distinct().Count() == v.Count)
            .WithMessage("A specification is given twice.");
        RuleForEach(x => x.Values).ChildRules(value =>
        {
            value.RuleFor(v => v).Must(v => (v.OptionId is null) != (v.Text is null))
                .WithMessage("A value is an option or a text, not both and not neither.");
            value.RuleFor(v => v.Text).Must(t => !string.IsNullOrWhiteSpace(t)).When(v => v.Text is not null)
                .WithMessage("A text value cannot be empty.").MaximumLength(200);
        });
    }
}

public class SetProductSpecificationsCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    ISpecificationRepository specifications,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization)
    : IRequestHandler<SetProductSpecificationsCommand, List<ProductSpecificationResponse>>
{
    private readonly IProductRepository _products = products;
    private readonly ICategoryRepository _categories = categories;
    private readonly ISpecificationRepository _specifications = specifications;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;

    public async Task<List<ProductSpecificationResponse>> Handle(
        SetProductSpecificationsCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027).
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var applicable = await Applicable.ToCategoryAsync(product.CategoryId, _categories, _specifications, cancellationToken);
        var byId = applicable.ToDictionary(s => s.Id);
        var failures = new List<ValidationFailure>();
        var values = new List<ProductSpecification>();

        for (var i = 0; i < request.Values.Count; i++)
        {
            var input = request.Values[i];
            var field = $"Values[{i}]";

            if (!byId.TryGetValue(input.SpecificationId, out var specification))
            {
                failures.Add(new(field, "This specification does not apply to the product's category."));
                continue;
            }

            if (specification.Kind == SpecificationKind.Choice)
            {
                if (input.OptionId is not { } optionId || specification.Options.All(o => o.Id != optionId))
                {
                    failures.Add(new(field, $"\"{specification.Name}\" is a choice: pick one of its options."));
                    continue;
                }

                values.Add(new() { ProductId = product.Id, SpecificationId = specification.Id, OptionId = optionId });
            }
            else
            {
                if (input.Text is null)
                {
                    failures.Add(new(field, $"\"{specification.Name}\" is a text: write the value."));
                    continue;
                }

                values.Add(new() { ProductId = product.Id, SpecificationId = specification.Id, Text = input.Text.Trim() });
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var current = await _specifications.ValuesOfAsync(product.Id, cancellationToken);
        var unchanged = current.Count == values.Count && values.All(v => current.Any(c =>
            c.SpecificationId == v.SpecificationId && c.OptionId == v.OptionId && c.Text == v.Text));

        if (!unchanged)
        {
            await _specifications.ReplaceValuesAsync(product.Id, values, cancellationToken);

            // What a shopper reads (specs/045): a seller's change to an approved product goes back to review.
            await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
            await _audit.RecordAsync(
                AuditCategory.Catalog, "ProductSpecificationsSet", "Product", product.Id.ToString(),
                $"Specifications of \"{product.Name}\" set",
                current.Select(c => new { c.SpecificationId, c.OptionId, c.Text }),
                values.Select(c => new { c.SpecificationId, c.OptionId, c.Text }),
                cancellationToken: cancellationToken);
            await _specifications.SaveChangesAsync(cancellationToken);
        }

        return Applicable.Table(applicable, values, _language.Current, _localization.DefaultLanguage);
    }
}
