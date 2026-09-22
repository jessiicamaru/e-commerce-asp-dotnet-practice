using Ecommerce.Catalog.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Products.Images.GetProductImage;

/// <summary>A product's current image, for anyone (specs/019). <c>null</c> when there is none.</summary>
public record GetProductImageQuery(Guid ProductId) : IRequest<ProductImage?>;

/// <param name="Version">What the address's <c>v</c> must equal for the response to be cacheable for good.</param>
public sealed record ProductImage(Stream Content, string ContentType, string Version);

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
        if (product is null || ProductImageKey.For(product) is not { } key)
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

        return new ProductImage(content, product.ImageContentType!, ProductImageKey.Version(product.ImageUpdatedAt!.Value));
    }
}
