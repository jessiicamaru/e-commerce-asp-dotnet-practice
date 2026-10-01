using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Commands.UpdateProductDetails;

/// <summary>
/// The product's own name and description - its default-language text (specs/021) - and its category (specs/124,
/// #240). Until this a seller could change neither after listing: no endpoint wrote these columns.
/// </summary>
/// <remarks>
/// A seller changing any of the three on an approved product sends it back to review (specs/045), the category too:
/// it decides which shoppers find the product and what it claims to be. Saving the same values again changes nothing
/// and sends nothing back.
/// </remarks>
public record UpdateProductDetailsCommand(Guid ProductId, string Name, string? Description, Guid CategoryId)
    : IRequest<ProductResponse>;

public class UpdateProductDetailsCommandValidator : AbstractValidator<UpdateProductDetailsCommand>
{
    public UpdateProductDetailsCommandValidator()
    {
        // The same limits as listing it (CreateProductCommandValidator).
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must be less than 200 characters.");
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Product description must be less than 2000 characters.");
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Product category ID is required.");
    }
}

public class UpdateProductDetailsCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    ICurrentUser currentUser,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IAuditTrail audit)
    : IRequestHandler<UpdateProductDetailsCommand, ProductResponse>
{
    private readonly IProductRepository _products = products;
    private readonly ICategoryRepository _categories = categories;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;
    private readonly IAuditTrail _audit = audit;

    public async Task<ProductResponse> Handle(UpdateProductDetailsCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027).
        SellerOwnership.RequireCanWrite(product, _currentUser);

        if (await _categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            throw new ValidationException([new ValidationFailure(nameof(request.CategoryId), "No such category.")]);

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var before = new { product.Name, product.Description, product.CategoryId };

        if (before.Name != name || before.Description != description || before.CategoryId != request.CategoryId)
        {
            product.Name = name;
            product.Description = description;
            product.CategoryId = request.CategoryId;
            product.UpdatedAt = DateTime.UtcNow;

            await _audit.RecordAsync(
                AuditCategory.Catalog, "ProductDetailsEdited", "Product", product.Id.ToString(),
                $"Edited the name, description or category of \"{name}\"",
                before, new { Name = name, Description = description, request.CategoryId },
                cancellationToken: cancellationToken);
            await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
            await _products.SaveChangesAsync(cancellationToken);
        }

        return ProductResponse.WithVariants(product, _language.Current, _localization.DefaultLanguage);
    }
}
