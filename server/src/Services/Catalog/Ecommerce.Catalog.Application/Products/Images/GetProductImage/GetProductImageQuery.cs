using Ecommerce.Catalog.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Products.Images.GetProductImage;

/// <summary>
/// A product's current image (specs/019). <c>null</c> when there is none - and, since specs/081, when the product is
/// off the shelf and <paramref name="Key"/> is not the image's own key.
/// </summary>
public record GetProductImageQuery(Guid ProductId, Guid? Key = null) : IRequest<ProductImage?>;

/// <param name="Version">What the address's <c>v</c> must equal for the response to be cacheable for good.</param>
/// <param name="Public">
/// False for the image of a product off the shelf: served to a keyed address, and never to a shared cache.
/// </param>
public sealed record ProductImage(Stream Content, string ContentType, string Version, bool Public = true);

public class GetProductImageQueryHandler(
    IProductRepository products,
    IProductImageStore store,
    ILogger<GetProductImageQueryHandler> logger)
    : IRequestHandler<GetProductImageQuery, ProductImage?>
{
    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ILogger<GetProductImageQueryHandler> _logger = logger;

    public async Task<ProductImage?> Handle(GetProductImageQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null
            || ProductImageKey.For(product) is not { } key
            || !ProductImageKey.MayServe(product, product.ImageAccessKey, request.Key))
        {
            return null;
        }

        var content = await _store.OpenReadAsync(key, cancellationToken);
        if (content is null)
        {
            // The row names a file the store does not have. The write-switch-delete order is meant to
            // make this impossible, so it is worth a line that someone will see.
            _logger.LogError("Product {ProductId} names image {Key}, which the store does not have.", product.Id, key);
            return null;
        }

        return new ProductImage(content, product.ImageContentType!, ProductImageKey.Version(product.ImageUpdatedAt!.Value), product.OnShelf);
    }
}
