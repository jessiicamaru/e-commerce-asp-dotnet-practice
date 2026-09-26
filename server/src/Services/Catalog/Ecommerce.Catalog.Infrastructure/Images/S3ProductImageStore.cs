using System.Net;
using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ecommerce.Catalog.Application.Common.Interfaces;

namespace Ecommerce.Catalog.Infrastructure.Images;

/// <summary>Where the bucket is and who Catalog is to it (specs/079). Every one of them is required.</summary>
public sealed class S3ImageStoreOptions
{
    public string ServiceUrl { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Signed into every request; a self-hosted server accepts any, AWS wants the bucket's own.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>How many keys one listing page asks for - 1000 is S3's own ceiling; a test pages with 2.</summary>
    public int PageSize { get; set; } = 1000;

    public IEnumerable<string> Problems()
    {
        if (string.IsNullOrWhiteSpace(ServiceUrl)) yield return "ProductImages:S3:ServiceUrl is not set.";
        if (string.IsNullOrWhiteSpace(Bucket)) yield return "ProductImages:S3:Bucket is not set.";
        if (string.IsNullOrWhiteSpace(AccessKey)) yield return "ProductImages:S3:AccessKey is not set.";
        if (string.IsNullOrWhiteSpace(SecretKey)) yield return "ProductImages:S3:SecretKey is not set.";
        if (PageSize is < 1 or > 1000) yield return "ProductImages:S3:PageSize must be from 1 to 1000.";
    }
}

/// <summary>
/// Product images in an S3-compatible bucket (specs/079, #114) - SeaweedFS in development, real S3 anywhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared by every Catalog instance.</b> This is what the directory store could not be: an image one instance
/// stored is served by the next, and the orphan report reads the one bucket they all write to.
/// </para>
/// <para>
/// Path-style addressing (<c>host/bucket/key</c>), which self-hosted servers need and AWS still accepts.
/// </para>
/// </remarks>
public sealed class S3ProductImageStore : IProductImageStore, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly S3ImageStoreOptions _options;

    public S3ProductImageStore(S3ImageStoreOptions options)
    {
        var problems = options.Problems().ToList();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Product images cannot be stored in S3: " + string.Join(" ", problems));
        }

        _options = options;
        _s3 = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config { ServiceURL = options.ServiceUrl, ForcePathStyle = true, AuthenticationRegion = options.Region });
    }

    public string Bucket => _options.Bucket;

    public bool SharedAcrossInstances => true;

    /// <summary>
    /// Creates the bucket if it is missing, then writes and deletes a probe - so a server that is unreachable, a
    /// wrong secret or a read-only identity stops the service at startup rather than failing the first upload. The
    /// server may still be starting when Catalog is, so it keeps trying for <paramref name="patience"/>.
    /// </summary>
    public async Task EnsureReadyAsync(TimeSpan patience, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + patience;
        while (true)
        {
            try
            {
                if (!await BucketExistsAsync(cancellationToken))
                {
                    await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
                }

                var probe = $".write-probe-{Guid.NewGuid():N}";
                await _s3.PutObjectAsync(new PutObjectRequest { BucketName = _options.Bucket, Key = probe, ContentBody = "" }, cancellationToken);
                await _s3.DeleteObjectAsync(_options.Bucket, probe, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is AmazonServiceException or HttpRequestException or IOException && DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex) when (ex is AmazonServiceException or HttpRequestException or IOException)
            {
                throw new InvalidOperationException(
                    $"Product images cannot be stored: bucket '{_options.Bucket}' at {_options.ServiceUrl} could not be written ({ex.Message}). "
                    + "Check ProductImages:S3 and that the S3 server is running.", ex);
            }
        }
    }

    public async Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ProductImageKeys.EnsureSafe(key);

        using var body = new MemoryStream(content.ToArray(), writable: false);
        try
        {
            // Only when the key is new, as the directory store's move is: one PUT is all of it or nothing.
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = body,
                ContentType = ProductImageKeys.ContentType(key),
                IfNoneMatch = "*",
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new IOException($"The product image '{key}' already exists.", ex);
        }
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ProductImageKeys.EnsureSafe(key);

        try
        {
            using var response = await _s3.GetObjectAsync(_options.Bucket, key, cancellationToken);

            // Into memory: an image is at most ProductImageKey.MaxBytes, and the response owns a connection that
            // must be given back rather than held for as long as a slow client takes to read it.
            var copy = new MemoryStream(capacity: (int)Math.Max(0, response.ContentLength));
            await response.ResponseStream.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            return copy;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ProductImageKeys.EnsureSafe(key);

        // S3 answers a delete of a missing key with success - the same "not an error" the directory gives.
        await _s3.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
    }

    public async IAsyncEnumerable<StoredImage> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? continuation = null;
        do
        {
            // A page at a time, yielded as it arrives: the bucket is never held in memory (specs/033's rule).
            var page = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                MaxKeys = _options.PageSize,
                ContinuationToken = continuation,
            }, cancellationToken);

            foreach (var item in page.S3Objects ?? [])
            {
                // The write probe, and anything else dot-prefixed: the store's own bookkeeping, never an image.
                if (item.Key.StartsWith('.'))
                {
                    continue;
                }

                yield return new StoredImage(item.Key, item.Size ?? 0, item.LastModified is { } at ? new DateTimeOffset(at.ToUniversalTime()) : DateTimeOffset.MinValue);
            }

            continuation = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (continuation is not null);
    }

    private async Task<bool> BucketExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _s3.GetBucketLocationAsync(_options.Bucket, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public void Dispose() => _s3.Dispose();
}
