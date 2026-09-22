using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Contracts.Catalog;
using MassTransit;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Shared.Money;
using FluentValidation;
using Microsoft.Extensions.Options;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;

/// <summary>
/// Adds another shape of an existing product: a kit, a colour, a size (specs/020).
/// </summary>
/// <remarks>
/// The price comes from the administrator, never from a shopper - the whole point of feature 009.
/// </remarks>
public record AddProductVariantCommand(
    Guid ProductId,
    string Sku,
    decimal Price,
    List<VariantOptionInput> Options
) : IRequest<VariantResponse>;

public record VariantOptionInput(string Name, string Value);

public class AddProductVariantCommandValidator : AbstractValidator<AddProductVariantCommand>
{
    public AddProductVariantCommandValidator(IOptions<CurrencyOptions> money)
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Price).GreaterThan(0);

        // The price on these commands is the DEFAULT currency's (specs/022). A price of 9.99 dong is
        // not a price, and nothing downstream would round it away: a subtotal is a unit price times
        // an integer.
        RuleFor(x => x.Price).MustFitTheCurrency(money.Value, _ => money.Value.DefaultCurrency);
        RuleForEach(x => x.Options).ChildRules(option =>
        {
            option.RuleFor(o => o.Name).NotEmpty().MaximumLength(50);
            option.RuleFor(o => o.Value).NotEmpty().MaximumLength(100);
        });
        RuleFor(x => x.Options)
            .Must(options => options.Select(o => o.Name.Trim().ToLowerInvariant()).Distinct().Count() == options.Count)
            .WithMessage("An option name can only appear once on a variant.");
    }
}

public class AddProductVariantCommandHandler(
    IProductRepository products,
    IPublishEndpoint publishEndpoint,
    ICurrentUser currentUser)
    : IRequestHandler<AddProductVariantCommand, VariantResponse>
{
    private readonly IProductRepository _products = products;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<VariantResponse> Handle(AddProductVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var sku = request.Sku.Trim();

        if (await _products.VariantSkuExistsAsync(sku, cancellationToken))
        {
            throw new ConflictException($"A variant with SKU '{sku}' already exists.");
        }

        var options = request.Options
            .Select(option => new VariantOption
            {
                Id = Guid.CreateVersion7(),
                Name = option.Name.Trim(),
                Value = option.Value.Trim(),
            })
            .ToList();

        // Two shapes a customer cannot tell apart are not two shapes. Checked here rather than in the
        // database: no constraint can say "this variant's SET of options differs from its siblings'"
        // without a trigger, so a simultaneous pair can still slip through (research D5). The unique
        // sku is what stops the same number being sold twice.
        var fingerprint = Fingerprint(options);

        if (product.Variants.Any(existing => Fingerprint(existing.Options) == fingerprint))
        {
            throw new ConflictException(
                options.Count == 0
                    ? "This product already has a variant with no options."
                    : $"This product already has a variant with {ProductVariant.Summarise(options)}.");
        }

        var variant = new ProductVariant
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Sku = sku,
            Price = request.Price,
            Options = options,
            OptionSummary = ProductVariant.Summarise(options),
        };

        foreach (var option in options)
        {
            option.VariantId = variant.Id;
        }

        await _products.AddVariantAsync(variant, cancellationToken);

        // Staged, then saved with the row in one transaction - the outbox pattern this project is
        // built on. Inventory holds stock per variant, so a new one has to be announced or it can
        // never be stocked.
        await _publishEndpoint.Publish(
            new ProductVariantCreatedEvent(product.Id, variant.Id, variant.Sku, DateTime.UtcNow),
            cancellationToken);

        await _products.SaveChangesAsync(cancellationToken);

        // The product's "from" price and availability follow its variants.
        await _products.RecomputeProductRollupAsync(product.Id, cancellationToken);

        return VariantResponse.From(variant);
    }

    /// <summary>The options as one comparable string, order- and case-insensitive.</summary>
    private static string Fingerprint(IEnumerable<VariantOption> options) =>
        string.Join('|', options
            .Select(option => $"{option.Name.Trim().ToLowerInvariant()}={option.Value.Trim().ToLowerInvariant()}")
            .OrderBy(pair => pair, StringComparer.Ordinal));
}
