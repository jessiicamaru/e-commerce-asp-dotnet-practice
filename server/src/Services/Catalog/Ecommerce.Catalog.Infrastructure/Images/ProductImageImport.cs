using Ecommerce.Catalog.Application.Common.Interfaces;

namespace Ecommerce.Catalog.Infrastructure.Images;

/// <summary>What one import did - copied, already there, or failed with the key and the reason.</summary>
public sealed record ImageImportResult(int Copied, int AlreadyThere, IReadOnlyList<string> Failed);

/// <summary>
/// Copies the images the old directory holds into the bucket (specs/079): the one-off move from the
/// <c>catalog_images</c> volume, run at startup while that volume is still mounted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotent by key.</b> A key the bucket has is left alone, so a restart copies nothing twice - and two
/// instances importing at once meet at the store's "only when new" (a 412 from S3), which counts as already there.
/// </para>
/// <para>
/// <b>Never deletes.</b> The directory stays exactly as it was: going back to the directory store is setting one
/// value, and nothing is lost if the move is abandoned halfway.
/// </para>
/// </remarks>
public static class ProductImageImport
{
    public static async Task<ImageImportResult> RunAsync(IProductImageStore from, IProductImageStore to, CancellationToken cancellationToken = default)
    {
        var copied = 0;
        var already = 0;
        var failed = new List<string>();

        await foreach (var image in from.ListAsync(cancellationToken))
        {
            try
            {
                await using (var existing = await to.OpenReadAsync(image.Key, cancellationToken))
                {
                    if (existing is not null)
                    {
                        already++;
                        continue;
                    }
                }

                await using var source = await from.OpenReadAsync(image.Key, cancellationToken);
                if (source is null)
                {
                    continue;   // gone from the directory since it was listed; nothing to copy
                }

                using var buffer = new MemoryStream();
                await source.CopyToAsync(buffer, cancellationToken);

                try
                {
                    await to.SaveAsync(image.Key, buffer.ToArray(), cancellationToken);
                    copied++;
                }
                catch (IOException)
                {
                    already++;   // another instance stored it between the read and the write
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add($"{image.Key}: {ex.Message}");
            }
        }

        return new ImageImportResult(copied, already, failed);
    }
}
