namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// One of a product's photographs after its cover (specs/160, #368). The cover stays on <see cref="Product"/>
/// (<c>ImageContentType</c>, <c>ImageUpdatedAt</c>, <c>ImageAccessKey</c>, specs/019), so everything that shows a
/// product's picture reads it as before.
/// </summary>
/// <remarks>
/// A photograph's bytes never change: a different photograph is a new row. Its key is <b>stored</b> rather than derived
/// (research D2), because a former cover moved into the gallery keeps the file it already had.
/// </remarks>
public class ProductPhoto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>Its place after the cover, from 0. Rewritten whole by a reorder.</summary>
    public int Position { get; set; }

    /// <summary><c>image/jpeg</c>, <c>image/png</c> or <c>image/webp</c>, decided from the bytes.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>The file's key in the image store.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>What an address must carry to see it while its product is off the shelf (specs/081).</summary>
    public Guid AccessKey { get; set; }

    public DateTime CreatedAt { get; set; }
}
