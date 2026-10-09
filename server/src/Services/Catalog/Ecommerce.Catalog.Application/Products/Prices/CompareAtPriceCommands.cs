using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Prices;

// A compare-at price per variant (specs/161, #369): what a price is compared against, struck through on the card and
// the product page. Display only - nothing that charges reads it - and, like any price, never a reason for review.

/// <summary>Sets what a variant's price in one currency is compared against, or replaces it.</summary>
public record SetCompareAtPriceCommand(Guid ProductId, Guid VariantId, string Currency, decimal Amount)
    : IRequest<VariantResponse>;

/// <summary>Stops showing a reduction in one currency. Quiet when there is none.</summary>
public record ClearCompareAtPriceCommand(Guid ProductId, Guid VariantId, string Currency) : IRequest;

public static class CompareAt
{
    /// <summary>
    /// A price at or above its compare-at is no longer reduced, so the compare-at goes with the change that set it
    /// (research D2) - the database's CHECK would refuse the save otherwise.
    /// </summary>
    public static void ClearIfNotBelow(ProductVariant variant)
    {
        if (variant.CompareAtPrice is { } compareAt && variant.Price >= compareAt)
        {
            variant.CompareAtPrice = null;
        }
    }

    /// <inheritdoc cref="ClearIfNotBelow(ProductVariant)"/>
    public static void ClearIfNotBelow(VariantPrice price)
    {
        if (price.CompareAtAmount is { } compareAt && price.Amount >= compareAt)
        {
            price.CompareAtAmount = null;
        }
    }
}

public class SetCompareAtPriceCommandValidator : AbstractValidator<SetCompareAtPriceCommand>
{
    public SetCompareAtPriceCommandValidator(IOptions<CurrencyOptions> money)
    {
        RuleFor(x => x.Currency).MustBeSupported(money.Value);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Amount).MustFitTheCurrency(money.Value, command => command.Currency);
    }
}

public class ClearCompareAtPriceCommandValidator : AbstractValidator<ClearCompareAtPriceCommand>
{
    public ClearCompareAtPriceCommandValidator(IOptions<CurrencyOptions> money)
    {
        RuleFor(x => x.Currency).MustBeSupported(money.Value);
    }
}

public class CompareAtPriceHandlers(
    IProductRepository products,
    IOptions<CurrencyOptions> money,
    ICurrentUser currentUser,
    IAuditTrail audit) :
    IRequestHandler<SetCompareAtPriceCommand, VariantResponse>,
    IRequestHandler<ClearCompareAtPriceCommand>
{
    private readonly IProductRepository _products = products;
    private readonly CurrencyOptions _money = money.Value;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task<VariantResponse> Handle(SetCompareAtPriceCommand request, CancellationToken cancellationToken)
    {
        var variant = await OwnedVariantAsync(request.ProductId, request.VariantId, cancellationToken);
        var currency = request.Currency.ToUpperInvariant();
        var before = CatalogAudit.Of(variant);

        // Beside the price it is compared against: there is no compare-at in a currency the variant is not sold in.
        if (IsDefault(currency))
        {
            RequireAbove(request.Amount, variant.Price, currency);
            variant.CompareAtPrice = request.Amount;
        }
        else
        {
            var price = variant.Prices.FirstOrDefault(p => p.Currency == currency)
                ?? throw Refused($"This shape has no price in {currency} to compare against. Set the price first.");
            RequireAbove(request.Amount, price.Amount, currency);
            price.CompareAtAmount = request.Amount;
        }

        variant.UpdatedAt = DateTime.UtcNow;
        await _audit.RecordAsync(
            AuditCategory.Catalog, "CompareAtPriceSet", "Variant", variant.Id.ToString(),
            $"{variant.Sku} compared against {request.Amount} {currency}", before, CatalogAudit.Of(variant),
            cancellationToken: cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);

        return VariantResponse.From(variant, currency: currency, defaultCurrency: _money.DefaultCurrency);
    }

    public async Task Handle(ClearCompareAtPriceCommand request, CancellationToken cancellationToken)
    {
        var variant = await OwnedVariantAsync(request.ProductId, request.VariantId, cancellationToken);
        var currency = request.Currency.ToUpperInvariant();
        var before = CatalogAudit.Of(variant);

        var price = IsDefault(currency) ? null : variant.Prices.FirstOrDefault(p => p.Currency == currency);
        var had = IsDefault(currency) ? variant.CompareAtPrice : price?.CompareAtAmount;
        if (had is null)
        {
            return;
        }

        if (price is null)
        {
            variant.CompareAtPrice = null;
        }
        else
        {
            price.CompareAtAmount = null;
        }

        variant.UpdatedAt = DateTime.UtcNow;
        await _audit.RecordAsync(
            AuditCategory.Catalog, "CompareAtPriceRemoved", "Variant", variant.Id.ToString(),
            $"{variant.Sku} no longer compared against {had} {currency}", before, CatalogAudit.Of(variant),
            cancellationToken: cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductVariant> OwnedVariantAsync(Guid productId, Guid variantId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{productId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027).
        SellerOwnership.RequireCanWrite(product, _currentUser);

        return product.Variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new NotFoundException($"Variant with ID '{variantId}' was not found on this product.");
    }

    private static void RequireAbove(decimal compareAt, decimal price, string currency)
    {
        if (compareAt <= price)
        {
            throw Refused($"A compare-at price must be above the price ({price} {currency}).");
        }
    }

    private bool IsDefault(string currency) =>
        string.Equals(currency, _money.DefaultCurrency, StringComparison.OrdinalIgnoreCase);

    private static ValidationException Refused(string message) =>
        new([new ValidationFailure(nameof(SetCompareAtPriceCommand.Amount), message)]);
}
