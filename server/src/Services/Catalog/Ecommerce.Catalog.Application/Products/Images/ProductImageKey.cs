using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Products.Images;

/// <summary>
/// Everything about an image that is derived from the product row: its store key, its version and its
/// address. Nothing about an image is stored anywhere else (specs/019 research D2).
/// </summary>
public static class ProductImageKey
{
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// A moment as PostgreSQL will store it. <c>timestamptz</c> keeps microseconds and .NET keeps
    /// 100-nanosecond ticks, so an untruncated value would come back from the database as a different
    /// version from the one written into the file's key.
    /// </summary>
    public static DateTime Truncate(DateTime utc) =>
        new(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);

    public static string Version(DateTime updatedAt) => updatedAt.Ticks.ToString();

    public static string For(Guid productId, DateTime updatedAt, ImageFormat format) =>
        $"{productId:N}-{Version(updatedAt)}.{format.Extension}";

    /// <summary>
    /// A VARIANT's image key (specs/032). Prefixed, and the prefix is load-bearing.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>The first variant of a product reuses the product's id</b> (specs/020) - measured at
    /// 12 out of 12 products in the live catalogue. Without this prefix that variant's key and its
    /// product's key differ only by their two <c>ImageUpdatedAt</c> values happening not to agree,
    /// and setting both in the same tick makes one image silently overwrite the other.
    /// <para>
    /// The PRODUCT form is deliberately left alone, so every image already on the volume keeps
    /// resolving and this ships without moving a file.
    /// </para>
    /// </remarks>
    public static string ForVariant(Guid variantId, DateTime updatedAt, ImageFormat format) =>
        $"variant-{variantId:N}-{Version(updatedAt)}.{format.Extension}";

    /// <summary>The variant's own image key, or <c>null</c> when it has none of its own.</summary>
    public static string? ForVariant(ProductVariant variant) =>
        variant.ImageUpdatedAt is { } at && ImageFormat.FromContentType(variant.ImageContentType) is { } format
            ? ForVariant(variant.Id, at, format)
            : null;

    /// <summary>
    /// The address for a variant's own image, or <c>null</c> when it has none. Callers fall back to
    /// the product's (specs/032 research D2) rather than showing nothing.
    /// </summary>
    public static string? UrlForVariant(ProductVariant variant) =>
        variant.ImageUpdatedAt is { } at
            ? $"/api/products/{variant.ProductId}/variants/{variant.Id}/image?v={Version(at)}"
            : null;

    /// <summary>The current image's key, or <c>null</c> when the product has none.</summary>
    public static string? For(Product product) =>
        product.ImageUpdatedAt is { } at && ImageFormat.FromContentType(product.ImageContentType) is { } format
            ? For(product.Id, at, format)
            : null;

    /// <summary>
    /// The address a client uses. The version in it changes with the image, which is what makes the
    /// long cache lifetime on a matching request safe.
    /// </summary>
    public static string? UrlFor(Product product) =>
        product.ImageUpdatedAt is { } at
            ? $"/api/products/{product.Id}/image?v={Version(at)}"
            : null;
}
