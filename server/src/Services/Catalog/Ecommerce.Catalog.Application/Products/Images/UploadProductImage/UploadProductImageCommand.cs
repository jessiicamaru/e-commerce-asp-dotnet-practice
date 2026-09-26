using Ecommerce.Catalog.Application.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Images.UploadProductImage;

/// <summary>Give a product its image, or replace the one it has (specs/019).</summary>
/// <param name="Length">The declared size, checked before anything is read.</param>
public record UploadProductImageCommand(Guid ProductId, Stream Content, long Length) : IRequest<ProductResponse>;

public class UploadProductImageCommandValidator : AbstractValidator<UploadProductImageCommand>
{
    public UploadProductImageCommandValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The file is empty.").OverridePropertyName("File")
            .LessThanOrEqualTo(ProductImageKey.MaxBytes)
            .WithMessage("The image is larger than 2 MB.").OverridePropertyName("File");
    }
}

/// <remarks>
/// <para>
/// <b>Write, switch, delete - in that order</b> (research D3). The new file is written under a new key
/// first. The row is then switched with a guarded statement. The old file is deleted only after that.
/// At every moment the row names a file that exists. A failure leaves at worst an orphan file, which is
/// waste, never a product pointing at nothing (FR-007).
/// </para>
/// <para>
/// The bytes decide the type (research D4). The declared length is checked by the validator and then
/// again while reading, because a length is also a claim.
/// </para>
/// </remarks>
public class UploadProductImageCommandHandler(
    IProductRepository products,
    IProductImageStore store,
    ICurrentUser currentUser,
    ILogger<UploadProductImageCommandHandler> logger,
    IAuditTrail audit)
    : IRequestHandler<UploadProductImageCommand, ProductResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UploadProductImageCommandHandler> _logger = logger;

    public async Task<ProductResponse> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027): a 403 would
        // confirm the id is real and that it belongs to someone.
        SellerOwnership.RequireCanWrite(product, _currentUser);

        var bytes = await ReadAtMostAsync(request.Content, ProductImageKey.MaxBytes, cancellationToken);

        var format = ImageFormat.Detect(bytes.Span)
            ?? throw Refused("The file is not a JPEG, PNG or WebP image.");

        var previousKey = ProductImageKey.For(product);
        var seen = product.ImageUpdatedAt;

        // A new version is always later than the one it replaces, so the new key never collides with it.
        var now = ProductImageKey.Truncate(DateTime.UtcNow);
        if (seen is { } previous && now <= previous)
        {
            now = previous.AddTicks(10);
        }

        var newKey = ProductImageKey.For(product.Id, now, format);

        // 1. Write. Nothing points at it yet.
        await _store.SaveAsync(newKey, bytes, cancellationToken);

        // 2. Switch - only if nobody else switched it since we looked.
        // A new image, a new key (specs/081): an address handed out for the old one opens nothing new.
        var access = Guid.NewGuid();
        var switched = await _products.TrySetImageAsync(product.Id, seen, format.ContentType, now, access, cancellationToken);

        if (switched == 0)
        {
            await DeleteQuietlyAsync(newKey, "an image that lost a concurrent replacement");
            throw new ConflictException("The product's image was changed by someone else meanwhile. Try again.");
        }

        await _audit.RecordAsync(
            AuditCategory.Catalog, "ProductImageSet", "Product", product.Id.ToString(),
            $"New photograph for \"{product.Name}\"", cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        // 3. Delete what the row no longer names.
        if (previousKey is not null)
        {
            await DeleteQuietlyAsync(previousKey, "a replaced image");
        }

        product.ImageContentType = format.ContentType;
        product.ImageUpdatedAt = now;
        product.ImageAccessKey = access;
        return ProductResponse.From(product);
    }

    private async Task DeleteQuietlyAsync(string key, string what)
    {
        // Deleting is tidying up. The row is already right, so a failure here is waste, not an error.
        try
        {
            await _store.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete {What} ({Key}); it is left behind as an orphan.", what, key);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAtMostAsync(Stream content, int max, CancellationToken cancellationToken)
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
