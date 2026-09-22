namespace Ecommerce.Catalog.Application.Products.Images;

/// <summary>
/// The image types a product may have, recognised by their leading bytes (specs/019 research D4).
/// </summary>
/// <remarks>
/// <para>
/// <b>The client's Content-Type and the file name are never consulted.</b> A header is a claim, and an
/// HTML or script file served from the shop's own origin as though it were a picture is how an upload
/// endpoint becomes stored XSS.
/// </para>
/// <para>
/// <b>SVG is deliberately absent</b>: it is an image format that can carry script.
/// </para>
/// </remarks>
public sealed record ImageFormat(string ContentType, string Extension)
{
    public static readonly ImageFormat Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageFormat Png = new("image/png", "png");
    public static readonly ImageFormat WebP = new("image/webp", "webp");

    public static readonly IReadOnlyList<ImageFormat> All = [Jpeg, Png, WebP];

    /// <summary>How many leading bytes <see cref="Detect"/> needs.</summary>
    public const int SignatureLength = 12;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The format these bytes begin with, or <c>null</c> if it is none of the accepted ones.</summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return Jpeg;
        }

        if (head.StartsWith(PngSignature))
        {
            return Png;
        }

        // "RIFF", four bytes of length, "WEBP".
        if (head.Length >= 12
            && head[..4].SequenceEqual("RIFF"u8)
            && head.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        return null;
    }

    public static ImageFormat? FromContentType(string? contentType) =>
        All.FirstOrDefault(f => f.ContentType == contentType);
}
