using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Images.RemoveProductImage;

/// <summary>Take a product's image away (specs/019). Removing an image that is not there is quiet.</summary>
public record RemoveProductImageCommand(Guid ProductId) : IRequest;

/// <remarks>
/// The same order as a replacement: switch the row first, delete the file after, so the row never
/// names a file that is gone.
/// </remarks>
public class RemoveProductImageCommandHandler(
    IProductRepository products,
    IProductImageStore store,
    ICurrentUser currentUser,
    ILogger<RemoveProductImageCommandHandler> logger,
    IAuditTrail audit)
    : IRequestHandler<RemoveProductImageCommand>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RemoveProductImageCommandHandler> _logger = logger;

    public async Task Handle(RemoveProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var key = ProductImageKey.For(product);
        if (key is null)
        {
            return;
        }

        if (await _products.TrySetImageAsync(product.Id, product.ImageUpdatedAt, null, null, cancellationToken) == 0)
        {
            throw new ConflictException("The product's image was changed by someone else meanwhile. Try again.");
        }

await _audit.RecordAsync(
    AuditCategory.Catalog, "ProductImageRemoved", "Product", product.Id.ToString(),
    $"Removed the photograph of \"{product.Name}\"", cancellationToken: cancellationToken);
await _products.SaveChangesAsync(cancellationToken);
        try
        {
            await _store.DeleteAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete removed image {Key}; it is left behind as an orphan.", key);
        }
    }
}
