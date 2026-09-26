using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Products.Availability;

public class RecordStockAvailabilityCommandHandler(
    IProductRepository productRepository,
    ILogger<RecordStockAvailabilityCommandHandler> logger,
    ISavedProductRepository saved,
    INotifier notifier,
    IEmailSender email
) : IRequestHandler<RecordStockAvailabilityCommand, bool>
{
    private readonly ISavedProductRepository _saved = saved;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ILogger<RecordStockAvailabilityCommandHandler> _logger = logger;

    public async Task<bool> Handle(RecordStockAvailabilityCommand request, CancellationToken cancellationToken)
    {
        // The VARIANT is what has stock (specs/020). The product's own flag is then recomputed from
        // its variants - it is a rollup, not a second source.
        var rowsAffected = await _productRepository.TryRecordVariantAvailabilityAsync(
            request.VariantId,
            request.IsAvailable,
            request.ObservedAt,
            cancellationToken);

        if (rowsAffected > 0)
        {
            var variant = await _productRepository.GetVariantAsync(request.VariantId, cancellationToken);

            if (variant is not null
                && await _productRepository.RecomputeProductRollupAsync(variant.ProductId, cancellationToken))
            {
                await TellWhoSavedItAsync(variant.ProductId, cancellationToken);
            }

            _logger.LogInformation(
                "Variant {VariantId} of product {ProductId} recorded as {Availability} (observed {ObservedAt:o}).",
                request.VariantId,
                request.ProductId,
                request.IsAvailable ? "available" : "unavailable",
                request.ObservedAt);

            return true;
        }

        // Zero rows has two causes that look identical from here and mean different things to
        // whoever reads the log. One extra read, only on this path, tells them apart.
        var exists = await _productRepository.GetVariantAsync(request.VariantId, cancellationToken) is not null;

        if (exists)
        {
            _logger.LogInformation(
                "Product {ProductId} already holds an observation at or after {ObservedAt:o}; "
                + "announcement ignored. Ordinary — the broker redelivers, and messages overtake "
                + "each other.",
                request.ProductId,
                request.ObservedAt);
        }
        else
        {
            _logger.LogWarning(
                "Availability announcement for product {ProductId}, which this catalogue does not "
                + "hold. Discarded rather than retried: the product will not appear later, and "
                + "redelivering forever would fault the endpoint.",
                request.ProductId);
        }

        return false;
    }

    /// <summary>
    /// The product came back in stock (specs/075): each shopper who saved it is told, once per flip - Inventory
    /// announces per variant and often, and only none-to-some is news. Through the consumer's outbox, so the notices
    /// commit with the availability they announce. Not for a product off the shelf: there is nothing to buy.
    /// </summary>
    private async Task TellWhoSavedItAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null || !product.OnShelf)
        {
            return;
        }

        var told = await SavedProductNotices.BackOnSaleAsync(product, _saved, _notifier, _email, cancellationToken);
        _logger.LogInformation("Product {ProductId} is back in stock; told {Count} shopper(s) who saved it.", productId, told);
    }
}
