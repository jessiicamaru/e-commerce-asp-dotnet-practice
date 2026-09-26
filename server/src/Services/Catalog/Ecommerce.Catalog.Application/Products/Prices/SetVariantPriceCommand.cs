using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Prices;

/// <summary>
/// Sets what a variant costs in one currency, or replaces it (specs/022).
/// </summary>
/// <remarks>
/// An upsert, like a translation: a price is a fact about a variant in a currency, and the second
/// attempt at the dollar price should be the dollar price rather than a conflict.
/// </remarks>
public record SetVariantPriceCommand(
    Guid ProductId,
    Guid VariantId,
    string Currency,
    decimal Amount
) : IRequest<VariantResponse>;

/// <summary>Stops selling this variant in this currency. Refused for the default currency.</summary>
public record RemoveVariantPriceCommand(Guid ProductId, Guid VariantId, string Currency) : IRequest;

public static class CurrencyRules
{
    /// <summary>
    /// A currency this shop prices in. Writing a price in one it does not is refused rather than
    /// stored: nothing would ever read it, and a row nobody reads is a claim the shop does not make.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeSupported<T>(
        this IRuleBuilder<T, string> rule, CurrencyOptions options) =>
        rule.Must(currency => options.Supported.Any(supported =>
                string.Equals(supported.Code, currency, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"Currency must be one of: {string.Join(", ", options.Supported.Select(c => c.Code))}.");

    /// <summary>
    /// The amount has to be one somebody could be charged in that currency: <b>9.99 dong is not a
    /// price</b>, and a price like it flows into a subtotal untouched, because a subtotal is a unit
    /// price times an integer and there is nothing there to round.
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> MustFitTheCurrency<T>(
        this IRuleBuilder<T, decimal> rule, CurrencyOptions options, Func<T, string> currencyOf) =>
        rule.Must((command, amount) => Find(options, currencyOf(command)) is not { } currency || currency.Fits(amount))
            .WithMessage((command, amount) =>
            {
                var currency = Find(options, currencyOf(command));
                return currency is null
                    ? "Unknown currency."
                    : $"{amount} is not an amount in {currency.Code}, which has "
                      + (currency.Decimals == 0 ? "no decimal places." : $"{currency.Decimals} decimal places.");
            });

    /// <summary>The configured currency for a code, or <c>null</c>.</summary>
    public static Currency? Find(CurrencyOptions options, string? code) =>
        code is null
            ? null
            : options.Supported
                .Where(supported => string.Equals(supported.Code, code, StringComparison.OrdinalIgnoreCase))
                .Select(supported => new Currency(supported.Code.ToUpperInvariant(), supported.Decimals))
                .FirstOrDefault();
}

public class SetVariantPriceCommandValidator : AbstractValidator<SetVariantPriceCommand>
{
    public SetVariantPriceCommandValidator(IOptions<CurrencyOptions> money)
    {
        RuleFor(x => x.Currency).MustBeSupported(money.Value);

        // Zero is a price, and it is a free camera. Anything that means "stop selling this in dollars"
        // is a DELETE, which says so.
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Amount).MustFitTheCurrency(money.Value, command => command.Currency);
    }
}

public class RemoveVariantPriceCommandValidator : AbstractValidator<RemoveVariantPriceCommand>
{
    public RemoveVariantPriceCommandValidator(IOptions<CurrencyOptions> money)
    {
        RuleFor(x => x.Currency).MustBeSupported(money.Value);
    }
}

public class SetVariantPriceCommandHandler(
    IProductRepository products,
    IOptions<CurrencyOptions> money,
    ICurrentUser currentUser,
    IAuditTrail audit,
    ISavedProductRepository saved,
    INotifier notifier,
    IEmailSender email)
    : IRequestHandler<SetVariantPriceCommand, VariantResponse>
{
    private readonly IAuditTrail _audit = audit;
    private readonly ISavedProductRepository _saved = saved;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;

    private readonly IProductRepository _products = products;
    private readonly CurrencyOptions _money = money.Value;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<VariantResponse> Handle(SetVariantPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        // A variant of somebody else's product is as good as missing (specs/020).
        var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId)
            ?? throw new NotFoundException($"Variant with ID '{request.VariantId}' was not found on this product.");

        var currency = request.Currency.ToUpperInvariant();
        var before = CatalogAudit.Of(variant);

        if (IsDefault(currency))
        {
            // The default currency's price lives on the variant itself - one source for one number.
            // Writing a row here as well would give the shop two answers to the same question, which
            // is the defect this table exists to avoid rather than to spread (research D2).
            variant.Price = request.Amount;
        }
        else
        {
            var price = variant.Prices.FirstOrDefault(p => p.Currency == currency);

            if (price is null)
            {
                variant.Prices.Add(new VariantPrice
                {
                    Id = Guid.CreateVersion7(),
                    VariantId = variant.Id,
                    Currency = currency,
                    Amount = request.Amount,
                });
            }
            else
            {
                price.Amount = request.Amount;
            }
        }

        variant.UpdatedAt = DateTime.UtcNow;
        await _audit.RecordAsync(
            AuditCategory.Catalog, "PriceSet", "Variant", variant.Id.ToString(),
            $"{variant.Sku} priced at {request.Amount} {currency}", before, CatalogAudit.Of(variant),
            cancellationToken: cancellationToken);
        // The product's "from" price is derived from its variants, so it is recomputed where they are - with the
        // change, in one transaction, telling whoever saved it if that put it back in stock (#182, specs/091).
        await _products.SaveAndRecomputeRollupAsync(
            product.Id, SavedProductNotices.WhenBackInStock(_products, product.Id, _saved, _notifier, _email), cancellationToken);

        return VariantResponse.From(variant, currency: currency, defaultCurrency: _money.DefaultCurrency);
    }

    private bool IsDefault(string currency) =>
        string.Equals(currency, _money.DefaultCurrency, StringComparison.OrdinalIgnoreCase);
}

public class RemoveVariantPriceCommandHandler(
    IProductRepository products,
    IOptions<CurrencyOptions> money,
    ICurrentUser currentUser,
    IAuditTrail audit)
    : IRequestHandler<RemoveVariantPriceCommand>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly CurrencyOptions _money = money.Value;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(RemoveVariantPriceCommand request, CancellationToken cancellationToken)
    {
        var currency = request.Currency.ToUpperInvariant();

        if (string.Equals(currency, _money.DefaultCurrency, StringComparison.OrdinalIgnoreCase))
        {
            // There is nothing to remove and nothing that could be: the default currency's price is
            // the variant's own column, and a variant with no price at all is not a shape this
            // catalogue has. Deactivate the variant instead - that is what "stop selling it" means.
            throw new ConflictException(
                $"'{currency}' is the shop's default currency, so its price cannot be removed. "
                + "Deactivate the variant instead.");
        }

        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId)
            ?? throw new NotFoundException($"Variant with ID '{request.VariantId}' was not found on this product.");

        var price = variant.Prices.FirstOrDefault(p => p.Currency == currency);

        if (price is null)
        {
            // Removing what is not there is not an error: the variant is already not sold in it.
            return;
        }

        var before = CatalogAudit.Of(variant);
        variant.Prices.Remove(price);
        await _audit.RecordAsync(
            AuditCategory.Catalog, "PriceRemoved", "Variant", variant.Id.ToString(),
            $"{variant.Sku} no longer sold in {currency}", before, CatalogAudit.Of(variant),
            cancellationToken: cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
    }
}
