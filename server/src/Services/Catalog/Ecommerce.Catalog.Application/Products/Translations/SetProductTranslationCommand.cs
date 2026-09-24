using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Translations;

/// <summary>
/// Gives a product its name and description in one language, or replaces them (specs/021).
/// </summary>
/// <remarks>
/// An upsert, because a translation is a fact about a product in a language rather than an event: the
/// second attempt at the Vietnamese name should be the Vietnamese name, not a conflict.
/// </remarks>
public record SetProductTranslationCommand(
    Guid ProductId,
    string Language,
    string Name,
    string? Description
) : IRequest<ProductResponse>;

public record RemoveProductTranslationCommand(Guid ProductId, string Language) : IRequest;

/// <summary>Translates one option: <c>Kit</c> → <c>Bộ</c>, <c>Body only</c> → <c>Chỉ thân máy</c>.</summary>
public record SetOptionTranslationCommand(
    Guid ProductId,
    Guid OptionId,
    string Language,
    string Name,
    string Value
) : IRequest;

public class SetProductTranslationCommandValidator : AbstractValidator<SetProductTranslationCommand>
{
    public SetProductTranslationCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public class SetOptionTranslationCommandValidator : AbstractValidator<SetOptionTranslationCommand>
{
    public SetOptionTranslationCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public class RemoveProductTranslationCommandValidator : AbstractValidator<RemoveProductTranslationCommand>
{
    public RemoveProductTranslationCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public static class LanguageRules
{
    /// <summary>
    /// A language this shop speaks. Writing a translation in one it does not is refused rather than
    /// stored: nothing would ever read it, and a row nobody reads is a lie about what the shop offers.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeSupported<T>(
        this IRuleBuilder<T, string> rule, LanguageOptions options) =>
        rule.Must(language => options.Supported.Contains(language, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Language must be one of: {string.Join(", ", options.Supported)}.");
}

public class SetProductTranslationCommandHandler(
    IProductRepository products,
    IOptions<LanguageOptions> localization,
    ICurrentUser currentUser,
    IAuditTrail audit)
    : IRequestHandler<SetProductTranslationCommand, ProductResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly LanguageOptions _localization = localization.Value;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<ProductResponse> Handle(SetProductTranslationCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var language = request.Language.ToLowerInvariant();
        var translation = product.Translations.FirstOrDefault(t => t.Language == language);
        var before = translation is null ? null : new { translation.Name, translation.Description };

        if (translation is null)
        {
            product.Translations.Add(new ProductTranslation
            {
                Id = Guid.CreateVersion7(),
                ProductId = product.Id,
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

        product.UpdatedAt = DateTime.UtcNow;
        await _audit.RecordAsync(
            AuditCategory.Catalog, "ProductTextEdited", "Product", product.Id.ToString(),
            $"Edited the {language} name and description of \"{product.Name}\"",
            before, new { Name = request.Name.Trim(), Description = request.Description?.Trim() },
            cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);

        return ProductResponse.WithVariants(product, language, _localization.DefaultLanguage);
    }
}

public class RemoveProductTranslationCommandHandler(IProductRepository products, ICurrentUser currentUser, IAuditTrail audit)
    : IRequestHandler<RemoveProductTranslationCommand>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(RemoveProductTranslationCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var language = request.Language.ToLowerInvariant();
        var translation = product.Translations.FirstOrDefault(t => t.Language == language);

        if (translation is null)
        {
            // Removing what is not there is not an error: the product already shows its default text,
            // which is what the caller asked for.
            return;
        }

        product.Translations.Remove(translation);
        await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
    }
}

public class SetOptionTranslationCommandHandler(IProductRepository products, ICurrentUser currentUser, IAuditTrail audit)
    : IRequestHandler<SetOptionTranslationCommand>
{
    private readonly IProductRepository _products = products;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task Handle(SetOptionTranslationCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        // An option of somebody else's product is as good as missing, like a variant of one (specs/020).
        var option = product.Variants
            .SelectMany(variant => variant.Options)
            .FirstOrDefault(option => option.Id == request.OptionId)
            ?? throw new NotFoundException($"Option with ID '{request.OptionId}' was not found on this product.");

        var language = request.Language.ToLowerInvariant();
        var translation = option.Translations.FirstOrDefault(t => t.Language == language);
        var before = translation is null ? null : new { translation.Language, translation.Name, translation.Value };

        if (translation is null)
        {
            option.Translations.Add(new VariantOptionTranslation
            {
                Id = Guid.CreateVersion7(),
                OptionId = option.Id,
                Language = language,
                Name = request.Name.Trim(),
                Value = request.Value.Trim(),
            });
        }
        else
        {
            translation.Name = request.Name.Trim();
            translation.Value = request.Value.Trim();
        }

        // On the record like every other translation, and - an option's words are shown on the product
        // page and frozen onto order lines - reviewed like any seller edit of what a shopper reads (#126).
        await _audit.RecordAsync(
            AuditCategory.Catalog, "OptionTranslated", "Product", product.Id.ToString(),
            $"Option of \"{product.Name}\" translated into {language}",
            before, new { Language = language, Name = request.Name.Trim(), Value = request.Value.Trim() },
            cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
    }
}
