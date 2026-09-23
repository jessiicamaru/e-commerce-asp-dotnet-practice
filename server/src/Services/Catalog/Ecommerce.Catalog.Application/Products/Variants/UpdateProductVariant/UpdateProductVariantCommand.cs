using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Shared.Money;
using FluentValidation;
using Microsoft.Extensions.Options;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;

/// <summary>
/// Re-prices a variant, or takes it off sale (specs/020).
/// </summary>
/// <remarks>
/// <b>The sku and the options are not changeable, and there is no delete.</b> Both are frozen onto
/// order lines, and an order has to keep describing what was bought. Taking a variant off sale is
/// <c>IsActive = false</c>, which leaves every order that refers to it intact.
/// </remarks>
public record UpdateProductVariantCommand(
    Guid ProductId,
    Guid VariantId,
    decimal Price,
    bool IsActive
) : IRequest<VariantResponse>;

public class UpdateProductVariantCommandValidator : AbstractValidator<UpdateProductVariantCommand>
{
    public UpdateProductVariantCommandValidator(IOptions<CurrencyOptions> money)
    {
        RuleFor(x => x.Price).GreaterThan(0);

        // The price on these commands is the DEFAULT currency's (specs/022). A price of 9.99 dong is
        // not a price, and nothing downstream would round it away: a subtotal is a unit price times
        // an integer.
        RuleFor(x => x.Price).MustFitTheCurrency(money.Value, _ => money.Value.DefaultCurrency);
    }
}

public class UpdateProductVariantCommandHandler(IProductRepository products, ICurrentUser currentUser,
    IAuditTrail audit)
    : IRequestHandler<UpdateProductVariantCommand, VariantResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<VariantResponse> Handle(UpdateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _products.GetVariantAsync(request.VariantId, cancellationToken);

        // A variant of a different product is as good as missing: the caller named a pair that does
        // not exist, and saying which half was wrong tells them about somebody else's catalogue.
        if (variant is null || variant.ProductId != request.ProductId)
        {
            throw new NotFoundException($"Variant with ID '{request.VariantId}' was not found.");
        }

        // This handler loads a VARIANT, so the owner is on the product it belongs to - which
        // GetVariantAsync includes for exactly this kind of question. Refused as not-found, and
        // worded as the variant being missing rather than the product, so the two refusals above and
        // here cannot be told apart (specs/027).
        if (variant.Product is null || !SellerOwnership.CanWrite(variant.Product, _currentUser))
        {
            throw new NotFoundException($"Variant with ID '{request.VariantId}' was not found.");
        }

        var before = CatalogAudit.Of(variant);
        variant.Price = request.Price;
        variant.IsActive = request.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;

        await _audit.RecordAsync(
            AuditCategory.Catalog, "VariantUpdated", "Variant", variant.Id.ToString(), $"Edited {variant.Sku}",
            before, CatalogAudit.Of(variant), cancellationToken: cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        await _products.RecomputeProductRollupAsync(variant.ProductId, cancellationToken);

        return VariantResponse.From(variant);
    }
}
