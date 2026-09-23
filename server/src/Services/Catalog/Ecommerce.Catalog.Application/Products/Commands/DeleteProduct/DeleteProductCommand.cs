using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Contracts.Catalog;
using Ecommerce.Shared.Exceptions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;

/// <summary>
/// Removes a product and every shape of it from the catalogue, for good (specs/024).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the rare operation, not the normal one.</b> Taking something off sale is deactivation:
/// the row stays, a cart holding it can explain itself, and a report still balances. This exists for
/// rows that should never have existed - the 94 <c>E2E Widget …</c> and <c>iPhone 16 Pro Max
/// 1790085352812</c> products that verify-saga.sh, verify-auth.sh and Bruno had left in the
/// catalogue, which no amount of deactivating would make less embarrassing.
/// </para>
/// <para>
/// <b>It takes the product's image with it</b> (specs/029, issue #66). It did not until then: the
/// row went and the bytes stayed on the volume forever, unreachable, with nothing to reclaim them.
/// Nothing broke, so nobody noticed - two orphans were found by listing the directory while
/// answering a question about where images are kept.
/// </para>
/// <para>
/// <b>An order is not harmed by it.</b> Every order froze the name, price, sku and option summary of
/// what was bought, so it still describes the purchase after the catalogue forgets the product. An
/// order that changed because a catalogue row was deleted would be the defect; this is the design
/// working.
/// </para>
/// </remarks>
public record DeleteProductCommand(Guid ProductId) : IRequest;

public class DeleteProductCommandHandler(
    IProductRepository products,
    IPublishEndpoint publishEndpoint,
    IProductImageStore store,
    ICurrentUser currentUser,
    ILogger<DeleteProductCommandHandler> logger) : IRequestHandler<DeleteProductCommand>
{
    private readonly IProductRepository _products = products;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IProductImageStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteProductCommandHandler> _logger = logger;

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027).
        SellerOwnership.RequireCanWrite(product, _currentUser);

        // Collected BEFORE the delete: after it there is nothing left to read them from, and
        // Inventory needs them to find the stock rows to drop.
        var variantIds = product.Variants.Select(v => v.Id).ToList();

        // The image key, for the same reason and in the same place. It is derived from columns on
        // the row, and after the delete there is no row to derive it from. Reading it late happens
        // to work while the detached entity is still in memory, which is a thing that works by
        // accident.
        var imageKey = ProductImageKey.For(product);

        // ...and every SHAPE's own photograph (specs/032). Collected in the same breath, because
        // adding variant images without extending this cleanup would have reintroduced the exact
        // leak specs/029 closed - the row goes, the bytes stay forever, and nothing breaks so
        // nobody notices.
        var variantImageKeys = product.Variants
            .Select(ProductImageKey.ForVariant)
            .OfType<string>()
            .ToList();

        _products.Remove(product);

        // Staged, then published, then saved - one transaction holding the deletion and the
        // announcement, so a crash cannot leave a deleted product nobody was told about, or an
        // announcement about a product still there (constitution III).
        await _publishEndpoint.Publish(
            new ProductDeletedEvent(product.Id, variantIds, DateTime.UtcNow), cancellationToken);

        await _products.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Product {ProductId} ({Sku}) and {VariantCount} variant(s) were DELETED from the catalogue.",
            product.Id, product.Sku, variantIds.Count);

        // AFTER the row, and outside the transaction, on purpose (specs/029 research D2 and D3).
        //
        // Deleting the bytes first would leave a live row naming a file that is gone - the failure
        // the upload ordering exists to prevent - and a rollback would make that permanent. This way
        // round the worst case is a file no row names, which is bounded to this handler rather than
        // to forever.
        //
        // And a store that throws must NOT fail the deletion. This endpoint exists for rows that
        // should never have existed (specs/024); a product that cannot be removed from the catalogue
        // because of a leftover PNG is a worse defect than the leak, and a read-only volume cannot be
        // retried into success. Same bargain, and the same wording, as RemoveProductImage.
        foreach (var key in variantImageKeys.Prepend(imageKey).OfType<string>())
        {
            try
            {
                await _store.DeleteAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Could not delete image {Key} of deleted product; it is left behind as an orphan.", key);
            }
        }
    }
}
