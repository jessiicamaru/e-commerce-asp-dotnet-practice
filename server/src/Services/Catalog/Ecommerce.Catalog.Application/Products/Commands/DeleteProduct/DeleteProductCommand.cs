using Ecommerce.Catalog.Application.Common.Interfaces;
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
    ILogger<DeleteProductCommandHandler> logger) : IRequestHandler<DeleteProductCommand>
{
    private readonly IProductRepository _products = products;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<DeleteProductCommandHandler> _logger = logger;

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.ProductId}' was not found.");

        // Collected BEFORE the delete: after it there is nothing left to read them from, and
        // Inventory needs them to find the stock rows to drop.
        var variantIds = product.Variants.Select(v => v.Id).ToList();

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
    }
}
