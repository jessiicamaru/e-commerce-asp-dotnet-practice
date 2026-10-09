using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Products.Images;

// A gallery of photographs per product (specs/160, #368). The cover stays the product's own photograph (specs/019);
// these are the ones after it, in `product_photos`.

/// <summary>Add a photograph. With no cover it becomes the cover, so a gallery never has photographs without one.</summary>
public record AddProductPhotoCommand(Guid ProductId, Stream Content, long Length) : IRequest<ProductResponse>;

/// <summary>Remove one photograph after the cover. The cover is removed by <c>DELETE .../image</c>.</summary>
public record RemoveProductPhotoCommand(Guid ProductId, Guid PhotoId) : IRequest;

/// <summary>Make a photograph the cover; the previous cover takes its place in the gallery.</summary>
public record MakeProductPhotoCoverCommand(Guid ProductId, Guid PhotoId) : IRequest<ProductResponse>;

/// <summary>
/// Remove the cover by promoting the first photograph - what <c>DELETE .../image</c> does when there are photographs,
/// rather than leave a gallery with no cover.
/// </summary>
public record PromoteFirstProductPhotoCommand(Guid ProductId) : IRequest;

/// <summary>The photographs after the cover in a new order: every one of them, each once (research D8).</summary>
public record ReorderProductPhotosCommand(Guid ProductId, List<Guid> PhotoIds) : IRequest<ProductResponse>;

/// <summary>One photograph's bytes - null when unknown, another product's, or off the shelf without its key.</summary>
public record GetProductPhotoQuery(Guid ProductId, Guid PhotoId, Guid? Key = null) : IRequest<ProductImage?>;

/// <summary>A photograph in a response: its id, to act on it, and its address.</summary>
public record ProductPhotoResponse(Guid Id, string Url);

public static class ProductGallery
{
    /// <summary>The cover and 9 more (research D4).</summary>
    public const int MaxPhotos = 10;

    public static ProductPhotoResponse Respond(ProductPhoto photo) => new(photo.Id, ProductImageKey.UrlForPhoto(photo));

    public static List<ProductPhotoResponse> Respond(IEnumerable<ProductPhoto> photos) =>
        photos.OrderBy(p => p.Position).ThenBy(p => p.CreatedAt).Select(Respond).ToList();
}

public class AddProductPhotoCommandValidator : AbstractValidator<AddProductPhotoCommand>
{
    public AddProductPhotoCommandValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The file is empty.").OverridePropertyName("File")
            .LessThanOrEqualTo(ProductImageKey.MaxBytes)
            .WithMessage("The image is larger than 2 MB.").OverridePropertyName("File");
    }
}

public class ReorderProductPhotosCommandValidator : AbstractValidator<ReorderProductPhotosCommand>
{
    public ReorderProductPhotosCommandValidator()
    {
        RuleFor(x => x.PhotoIds).NotNull();
        RuleFor(x => x.PhotoIds)
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("A photograph is named twice.")
            .When(x => x.PhotoIds is not null);
    }
}

/// <remarks>
/// <para>
/// <b>Write, switch, delete</b> - specs/019's order, for every photograph: bytes are written under a new key before any
/// row names them, and deleted only after no row does. A failure leaves at worst an orphan, which the report finds
/// (specs/033), never a row naming a missing file.
/// </para>
/// <para>
/// <b>Every change by a seller to an approved product sends it back to review</b> (research D5, specs/045's "any
/// photograph"), in the same commit as the change and its audit entry.
/// </para>
/// </remarks>
public class ProductGalleryHandlers(
    IProductRepository products,
    IProductImageStore store,
    ICurrentUser currentUser,
    IAuditTrail audit,
    ILogger<ProductGalleryHandlers> logger) :
    IRequestHandler<AddProductPhotoCommand, ProductResponse>,
    IRequestHandler<RemoveProductPhotoCommand>,
    IRequestHandler<MakeProductPhotoCoverCommand, ProductResponse>,
    IRequestHandler<PromoteFirstProductPhotoCommand>,
    IRequestHandler<ReorderProductPhotosCommand, ProductResponse>,
    IRequestHandler<GetProductPhotoQuery, ProductImage?>
{
    private readonly IProductRepository _products = products;
    private readonly IProductImageStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly ILogger<ProductGalleryHandlers> _logger = logger;

    public async Task<ProductResponse> Handle(AddProductPhotoCommand request, CancellationToken cancellationToken)
    {
        var product = await OwnedAsync(request.ProductId, cancellationToken);
        var photos = await _products.GetPhotosAsync(product.Id, cancellationToken);

        var count = photos.Count + (product.ImageUpdatedAt is null ? 0 : 1);
        if (count >= ProductGallery.MaxPhotos)
        {
            throw new ConflictException($"A product has at most {ProductGallery.MaxPhotos} photographs. Remove one first.");
        }

        var bytes = await ImageBytes.ReadAsync(request.Content, cancellationToken);
        var format = ImageFormat.Detect(bytes.Span)
            ?? throw ImageBytes.Refused("The file is not a JPEG, PNG or WebP image.");

        if (product.ImageUpdatedAt is null)
        {
            // No cover yet: this is the cover, through the same columns specs/019 writes.
            await SwitchCoverAsync(product, bytes, format, cancellationToken, async () =>
            {
                await _audit.RecordAsync(
                    AuditCategory.Catalog, "ProductImageSet", "Product", product.Id.ToString(),
                    $"New photograph for \"{product.Name}\"", cancellationToken: cancellationToken);
                await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
            });

            return await RespondAsync(product.Id, cancellationToken);
        }

        var now = ProductImageKey.Truncate(DateTime.UtcNow);
        var photo = new ProductPhoto
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Position = photos.Count == 0 ? 0 : photos.Max(p => p.Position) + 1,
            ContentType = format.ContentType,
            AccessKey = Guid.NewGuid(),
            CreatedAt = now,
        };
        photo.StorageKey = ProductImageKey.ForPhoto(photo.Id, now, format);

        // 1. Write. Nothing names it yet.
        await _store.SaveAsync(photo.StorageKey, bytes, cancellationToken);

        // 2. The row, its audit entry and the review, in one save.
        try
        {
            _products.AddPhoto(photo);
            await _audit.RecordAsync(
                AuditCategory.Catalog, "ProductPhotoAdded", "Product", product.Id.ToString(),
                $"New photograph in the gallery of \"{product.Name}\"", cancellationToken: cancellationToken);
            await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
            await _products.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteQuietlyAsync(photo.StorageKey, "a photograph whose row was not saved");
            throw;
        }

        return await RespondAsync(product.Id, cancellationToken);
    }

    public async Task Handle(RemoveProductPhotoCommand request, CancellationToken cancellationToken)
    {
        var product = await OwnedAsync(request.ProductId, cancellationToken);
        var photo = await PhotoOfAsync(product.Id, request.PhotoId, cancellationToken);

        _products.RemovePhoto(photo);
        await _audit.RecordAsync(
            AuditCategory.Catalog, "ProductPhotoRemoved", "Product", product.Id.ToString(),
            $"Removed a photograph from the gallery of \"{product.Name}\"", cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);

        // 3. Delete what no row names any more.
        await DeleteQuietlyAsync(photo.StorageKey, "a removed photograph");
    }

    public async Task<ProductResponse> Handle(MakeProductPhotoCoverCommand request, CancellationToken cancellationToken)
    {
        var product = await OwnedAsync(request.ProductId, cancellationToken);
        var chosen = await PhotoOfAsync(product.Id, request.PhotoId, cancellationToken);

        await PromoteAsync(product, chosen, keepOldCover: true, cancellationToken);
        return await RespondAsync(product.Id, cancellationToken);
    }

    public async Task Handle(PromoteFirstProductPhotoCommand request, CancellationToken cancellationToken)
    {
        var product = await OwnedAsync(request.ProductId, cancellationToken);
        var first = (await _products.GetPhotosAsync(product.Id, cancellationToken)).FirstOrDefault()
            ?? throw new ConflictException("The product has no other photograph to make its cover.");

        await PromoteAsync(product, first, keepOldCover: false, cancellationToken);
    }

    public async Task<ProductResponse> Handle(ReorderProductPhotosCommand request, CancellationToken cancellationToken)
    {
        var product = await OwnedAsync(request.ProductId, cancellationToken);
        var photos = await _products.GetPhotosAsync(product.Id, cancellationToken);

        // Exactly the current set, each once (the validator refused duplicates): a stale or partial list changes
        // nothing rather than leave a photograph without a place.
        if (request.PhotoIds.Count != photos.Count || photos.Any(p => !request.PhotoIds.Contains(p.Id)))
        {
            throw new ValidationException([new ValidationFailure(
                nameof(request.PhotoIds), "Name every photograph after the cover, each once.")]);
        }

        for (var i = 0; i < request.PhotoIds.Count; i++)
        {
            photos.Single(p => p.Id == request.PhotoIds[i]).Position = i;
        }

        await _audit.RecordAsync(
            AuditCategory.Catalog, "ProductPhotosReordered", "Product", product.Id.ToString(),
            $"New order for the gallery of \"{product.Name}\"", cancellationToken: cancellationToken);
        await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);

        return await RespondAsync(product.Id, cancellationToken);
    }

    public async Task<ProductImage?> Handle(GetProductPhotoQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        var photo = product is null ? null : await _products.GetPhotoAsync(request.PhotoId, cancellationToken);

        if (product is null || photo is null || photo.ProductId != product.Id
            || !ProductImageKey.MayServe(product, photo.AccessKey, request.Key))
        {
            return null;
        }

        var content = await _store.OpenReadAsync(photo.StorageKey, cancellationToken);
        if (content is null)
        {
            _logger.LogError("Photograph {PhotoId} of product {ProductId} names {Key}, which the store does not have.",
                photo.Id, product.Id, photo.StorageKey);
            return null;
        }

        // A photograph's bytes never change, so its id is its version (research D6).
        return new ProductImage(content, photo.ContentType, ProductImageKey.PhotoVersion(photo), product.OnShelf);
    }

    /// <summary>
    /// Makes <paramref name="chosen"/> the cover (research D3): its bytes are written under a new cover key, then in one
    /// transaction the cover columns are switched - only if the cover is still the one read - its row goes, and the old
    /// cover either takes its place (keeping its file) or is let go; its files are deleted after.
    /// </summary>
    private async Task PromoteAsync(Product product, ProductPhoto chosen, bool keepOldCover, CancellationToken cancellationToken)
    {
        var bytes = await ReadStoredAsync(chosen.StorageKey, cancellationToken);
        var format = ImageFormat.FromContentType(chosen.ContentType)
            ?? throw new InvalidOperationException($"Photograph {chosen.Id} has an unknown type {chosen.ContentType}.");

        var oldCoverKey = ProductImageKey.For(product);
        var oldCoverType = product.ImageContentType;

        await SwitchCoverAsync(product, bytes, format, cancellationToken, async () =>
        {
            _products.RemovePhoto(chosen);
            if (keepOldCover && oldCoverKey is not null && oldCoverType is not null)
            {
                _products.AddPhoto(new ProductPhoto
                {
                    Id = Guid.CreateVersion7(),
                    ProductId = product.Id,
                    Position = chosen.Position,
                    ContentType = oldCoverType,
                    StorageKey = oldCoverKey,
                    AccessKey = Guid.NewGuid(),
                    CreatedAt = ProductImageKey.Truncate(DateTime.UtcNow),
                });
            }

            await _audit.RecordAsync(
                AuditCategory.Catalog, keepOldCover ? "ProductCoverChosen" : "ProductImageRemoved", "Product",
                product.Id.ToString(),
                keepOldCover
                    ? $"Another photograph made the cover of \"{product.Name}\""
                    : $"Removed the photograph of \"{product.Name}\"; the next one is its cover",
                cancellationToken: cancellationToken);
            await ProductReview.AfterSellerEditAsync(product, _currentUser, _audit, cancellationToken);
        });

        await DeleteQuietlyAsync(chosen.StorageKey, "a photograph copied to the cover");
        if (!keepOldCover && oldCoverKey is not null)
        {
            await DeleteQuietlyAsync(oldCoverKey, "a removed cover");
        }
    }

    /// <summary>Writes a new cover and switches the product to it with <paramref name="stage"/>, in one transaction.</summary>
    private async Task SwitchCoverAsync(
        Product product, ReadOnlyMemory<byte> bytes, ImageFormat format, CancellationToken cancellationToken,
        Func<Task> stage)
    {
        var seen = product.ImageUpdatedAt;
        var now = ProductImageKey.Truncate(DateTime.UtcNow);
        if (seen is { } previous && now <= previous)
        {
            now = previous.AddTicks(10);
        }

        var newKey = ProductImageKey.For(product.Id, now, format);
        await _store.SaveAsync(newKey, bytes, cancellationToken);

        // A new image, a new key (specs/081): an address handed out for the old cover opens nothing new.
        var access = Guid.NewGuid();
        bool switched;
        try
        {
            switched = await _products.SwitchCoverAsync(
                product.Id, seen, format.ContentType, now, access, stage, cancellationToken);
        }
        catch
        {
            await DeleteQuietlyAsync(newKey, "a cover whose switch failed");
            throw;
        }

        if (!switched)
        {
            await DeleteQuietlyAsync(newKey, "a cover that lost a concurrent change");
            throw new ConflictException("The product's photographs were changed by someone else meanwhile. Try again.");
        }

        // The switch was a statement, not a tracked change: say so to the tracked product the response is built from.
        product.ImageContentType = format.ContentType;
        product.ImageUpdatedAt = now;
        product.ImageAccessKey = access;
    }

    private async Task<Product> OwnedAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        // Somebody else's listing is NOT FOUND, never forbidden (specs/027).
        SellerOwnership.RequireCanWrite(product, _currentUser);
        return product;
    }

    private async Task<ProductPhoto> PhotoOfAsync(Guid productId, Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await _products.GetPhotoAsync(photoId, cancellationToken);
        return photo is not null && photo.ProductId == productId
            ? photo
            : throw new NotFoundException("Photograph not found.");
    }

    private async Task<ProductResponse> RespondAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");
        var photos = await _products.GetPhotosAsync(productId, cancellationToken);
        return ProductResponse.From(product) with { Photos = ProductGallery.Respond(photos) };
    }

    private async Task<ReadOnlyMemory<byte>> ReadStoredAsync(string key, CancellationToken cancellationToken)
    {
        await using var content = await _store.OpenReadAsync(key, cancellationToken)
            ?? throw new ConflictException("That photograph's file is missing. Remove it and add it again.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private async Task DeleteQuietlyAsync(string key, string what)
    {
        // Tidying up. The rows are already right, so a failure here is waste, not an error.
        try
        {
            await _store.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete {What} ({Key}); it is left behind as an orphan.", what, key);
        }
    }
}

/// <summary>An upload read whole, refused when empty or over the limit - the declared length is only a claim.</summary>
internal static class ImageBytes
{
    public static async Task<ReadOnlyMemory<byte>> ReadAsync(Stream content, CancellationToken cancellationToken)
    {
        var max = ProductImageKey.MaxBytes;
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

    public static ValidationException Refused(string message) =>
        new([new ValidationFailure("File", message)]);
}
