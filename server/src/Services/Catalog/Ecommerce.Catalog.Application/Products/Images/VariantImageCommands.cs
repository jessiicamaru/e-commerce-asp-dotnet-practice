using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Images;

/// <summary>Give one shape of a product its own photograph, or replace it (specs/032).</summary>
/// <param name="Length">The declared size, checked before anything is read.</param>
public record UploadVariantImageCommand(Guid ProductId, Guid VariantId, Stream Content, long Length) : IRequest;

public class UploadVariantImageCommandValidator : AbstractValidator<UploadVariantImageCommand>
{
    public UploadVariantImageCommandValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The file is empty.").OverridePropertyName("File")
            .LessThanOrEqualTo(ProductImageKey.MaxBytes)
            .WithMessage("The image is larger than 2 MB.").OverridePropertyName("File");
    }
}

/// <summary>Take one shape's own photograph away. It then falls back to the product's.</summary>
public record RemoveVariantImageCommand(Guid ProductId, Guid VariantId) : IRequest;

/// <remarks>
/// <para>
/// <b>Write, switch, delete - in that order</b>, exactly as the product's image does (specs/019
/// research D3). The new file is written under a new key first, the row is switched with a guarded
/// statement, and the old file is deleted only after that. At every moment the row names a file that
/// exists; a failure leaves at worst an orphan, which is waste rather than a variant pointing at
/// nothing.
/// </para>
/// <para>
/// ⚠️ The key carries a <c>variant-</c> prefix because <b>the first variant of a product reuses the
/// product's id</b> (specs/020). Without it, that variant's key and its product's key would differ
/// only by their two timestamps happening not to agree.
/// </para>
/// </remarks>
public class UploadVariantImageCommandHandler(
    IProductRepository products,
    IProductImageStore store,
    ICurrentUser currentUser,
    ILogger<UploadVariantImageCommandHandler> logger,
    IAuditTrail audit) : IRequestHandler<UploadVariantImageCommand>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UploadVariantImageCommandHandler> _logger = logger;

    public async Task Handle(UploadVariantImageCommand request, CancellationToken cancellationToken)
    {
        var variant = await VariantImages.RequireOwnedVariantAsync(
            _products, _currentUser, request.ProductId, request.VariantId, cancellationToken);

        var bytes = await ReadAtMostAsync(request.Content, ProductImageKey.MaxBytes, cancellationToken);

        var format = ImageFormat.Detect(bytes.Span)
            ?? throw Refused("The file is not a JPEG, PNG or WebP image.");

        var previousKey = ProductImageKey.ForVariant(variant);
        var seen = variant.ImageUpdatedAt;

        // A new version is always later than the one it replaces, so the new key never collides.
        var now = ProductImageKey.Truncate(DateTime.UtcNow);
        if (seen is { } previous && now <= previous)
        {
            now = previous.AddTicks(10);
        }

        var newKey = ProductImageKey.ForVariant(variant.Id, now, format);

        await _store.SaveAsync(newKey, bytes, cancellationToken);

        var switched = await _products.TrySetVariantImageAsync(
            variant.Id, seen, format.ContentType, now, cancellationToken);

        if (switched == 0)
        {
            await DeleteQuietlyAsync(newKey, "an image that lost a concurrent replacement");
            throw new ConflictException("This shape's image was changed by someone else meanwhile. Try again.");
        }

await _audit.RecordAsync(
    AuditCategory.Catalog, "VariantImageSet", "Variant", variant.Id.ToString(),
    $"New photograph for {variant.Sku}", cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(variant.Product!, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        if (previousKey is not null)
        {
            await DeleteQuietlyAsync(previousKey, "a replaced variant image");
        }
    }

    private async Task DeleteQuietlyAsync(string key, string what)
    {
        // Tidying up. The row is already right, so a failure here is waste, not an error.
        try
        {
            await _store.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete {What} ({Key}); it is left behind as an orphan.", what, key);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAtMostAsync(
        Stream content, int max, CancellationToken cancellationToken)
    {
        var buffer = new byte[max + 1];
        var read = 0;

        while (read < buffer.Length)
        {
            var n = await content.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        if (read > max)
        {
            throw Refused("The image is larger than 2 MB.");
        }

        if (read == 0)
        {
            throw Refused("The file is empty.");
        }

        return buffer.AsMemory(0, read);
    }

    private static ValidationException Refused(string message) =>
        new([new ValidationFailure("File", message)]);
}

/// <remarks>The same order as a replacement: switch the row first, delete the file after.</remarks>
public class RemoveVariantImageCommandHandler(
    IProductRepository products,
    IProductImageStore store,
    ICurrentUser currentUser,
    ILogger<RemoveVariantImageCommandHandler> logger,
    IAuditTrail audit) : IRequestHandler<RemoveVariantImageCommand>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RemoveVariantImageCommandHandler> _logger = logger;

    public async Task Handle(RemoveVariantImageCommand request, CancellationToken cancellationToken)
    {
        var variant = await VariantImages.RequireOwnedVariantAsync(
            _products, _currentUser, request.ProductId, request.VariantId, cancellationToken);

        var key = ProductImageKey.ForVariant(variant);
        if (key is null)
        {
            // Removing what is not there is quiet, like the product's. The variant simply keeps
            // falling back to its product's picture.
            return;
        }

        if (await _products.TrySetVariantImageAsync(
                variant.Id, variant.ImageUpdatedAt, null, null, cancellationToken) == 0)
        {
            throw new ConflictException("This shape's image was changed by someone else meanwhile. Try again.");
        }

await _audit.RecordAsync(
    AuditCategory.Catalog, "VariantImageRemoved", "Variant", variant.Id.ToString(),
    $"Removed the photograph of {variant.Sku}", cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(variant.Product!, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        try
        {
            await _store.DeleteAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete removed variant image {Key}; it is left behind as an orphan.", key);
        }
    }
}

/// <summary>The lookup both writes share, and the refusal they share with it.</summary>
internal static class VariantImages
{
    /// <summary>
    /// The variant, once it is established that it exists, belongs to this product, and is the
    /// caller's to write.
    /// </summary>
    /// <remarks>
    /// <b>Every refusal here is the same 404.</b> A variant that does not exist, one belonging to a
    /// different product, and one belonging to a different seller all answer identically - a
    /// difference between them would let a caller map the catalogue by asking (specs/027).
    /// </remarks>
    public static async Task<Domain.Entities.ProductVariant> RequireOwnedVariantAsync(
        IProductRepository products,
        ICurrentUser currentUser,
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var variant = await products.GetVariantAsync(variantId, cancellationToken);

        var mine = variant is not null
            && variant.ProductId == productId
            && variant.Product is not null
            // CanWrite, not RequireCanWrite: that one throws a message naming the PRODUCT id, and a
            // refusal worded differently from "no such variant" is a refusal a caller can tell
            // apart - which is the whole thing the 404 exists to prevent.
            && SellerOwnership.CanWrite(variant.Product, currentUser);

        if (!mine)
        {
            throw new NotFoundException($"Variant with ID '{variantId}' was not found.");
        }

        return variant!;
    }
}
