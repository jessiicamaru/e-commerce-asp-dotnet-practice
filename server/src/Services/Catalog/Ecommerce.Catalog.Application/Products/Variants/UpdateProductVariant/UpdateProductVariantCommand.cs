using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Shared.Money;
using FluentValidation;
using Microsoft.Extensions.Options;
using MediatR;

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

public class UpdateProductVariantCommandHandler(IProductRepository products)
    : IRequestHandler<UpdateProductVariantCommand, VariantResponse>
{
    private readonly IProductRepository _products = products;

    public async Task<VariantResponse> Handle(UpdateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _products.GetVariantAsync(request.VariantId, cancellationToken);

        // A variant of a different product is as good as missing: the caller named a pair that does
        // not exist, and saying which half was wrong tells them about somebody else's catalogue.
        if (variant is null || variant.ProductId != request.ProductId)
        {
            throw new NotFoundException($"Variant with ID '{request.VariantId}' was not found.");
        }

        variant.Price = request.Price;
        variant.IsActive = request.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;

        await _products.SaveChangesAsync(cancellationToken);
        await _products.RecomputeProductRollupAsync(variant.ProductId, cancellationToken);

        return VariantResponse.From(variant);
    }
}
