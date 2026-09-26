using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Products.Images;

/// <summary>
/// One shape's own photograph, for anyone (specs/032). <c>null</c> when it has none of its own.
/// </summary>
/// <remarks>
/// <b>It does not fall back here.</b> The fallback lives in <c>VariantResponse.ImageUrl</c>, which
/// hands out the product's address when the variant has no picture - so a caller that reached this
/// route was told to, and answering with the product's bytes under a variant address would give two
/// addresses the same body and break the immutable cache the version exists for.
/// </remarks>
public record GetVariantImageQuery(Guid ProductId, Guid VariantId, Guid? Key = null) : IRequest<ProductImage?>;

public class GetVariantImageQueryHandler(
    IProductRepository products,
    IProductImageStore store,
    ILogger<GetVariantImageQueryHandler> logger) : IRequestHandler<GetVariantImageQuery, ProductImage?>
{
    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ILogger<GetVariantImageQueryHandler> _logger = logger;

    public async Task<ProductImage?> Handle(GetVariantImageQuery request, CancellationToken cancellationToken)
    {
        var variant = await _products.GetVariantAsync(request.VariantId, cancellationToken);

        if (variant is null
            || variant.ProductId != request.ProductId
            || ProductImageKey.ForVariant(variant) is not { } key
            || variant.Product is not { } product
            || !ProductImageKey.MayServe(product, variant.ImageAccessKey, request.Key))
        {
            return null;
        }

        var content = await _store.OpenReadAsync(key, cancellationToken);
        if (content is null)
        {
            // The row names a file the store does not have. Write-switch-delete is meant to make
            // this impossible, so it is worth a line somebody will see.
            _logger.LogError("Variant {VariantId} names image {Key}, which the store does not have.", variant.Id, key);
            return null;
        }

        return new ProductImage(
            content, variant.ImageContentType!, ProductImageKey.Version(variant.ImageUpdatedAt!.Value), product.OnShelf);
    }
}
