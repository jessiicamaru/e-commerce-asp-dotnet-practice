namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>
/// Where product image bytes live - outside the database (specs/019).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the seam object storage plugs into.</b> The first implementation is a directory, which
/// assumes one Catalog instance; two would each see only their own files. Replacing it with S3, Azure
/// Blob or MinIO changes this interface's implementation and nothing that calls it, as
/// <c>StubPaymentGateway</c> is Payment's seam for a real provider.
/// </para>
/// <para>
/// Keys come from <see cref="Products.Images.ProductImageKey"/>, derived from the product row, so the
/// row decides which file is current and the store never has to be asked.
/// </para>
/// </remarks>
public interface IProductImageStore
{
    /// <summary>Stores the bytes under a key that must not exist yet. Either all of it is stored, or nothing is.</summary>
    Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>The stored bytes, or <c>null</c> when there is nothing under that key.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes the key. Removing what is not there is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything the store holds (specs/033) - the only way to find images no row can name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Streaming, not a list.</b> This interface is the seam object storage plugs into and a
    /// bucket listing is paged; a signature returning the whole thing invites an implementation that
    /// buys the bucket into memory. The one caller compares each key against a set and keeps only
    /// the differences.
    /// </para>
    /// <para>
    /// <b>A store excludes its own bookkeeping.</b> The filesystem implementation writes a write
    /// probe at construction, and a crash at the wrong instant leaves one behind; it is not a
    /// product image, and only the store knows that.
    /// </para>
    /// </remarks>
    IAsyncEnumerable<StoredImage> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One thing the store holds.
/// </summary>
/// <param name="LastModified">
/// The STORE's timestamp, not the one in the key. The ticks in a key are the row's version, and an
/// orphan has no row - the file's own timestamp is the only thing that can say how old it is.
/// </param>
public readonly record struct StoredImage(string Key, long Size, DateTimeOffset LastModified);
