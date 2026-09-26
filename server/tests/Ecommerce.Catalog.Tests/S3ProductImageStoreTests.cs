using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Infrastructure.Images;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Product images in an S3-compatible bucket (specs/079, #114), against a real S3 server - SeaweedFS from
/// docker-compose.yml, or CI's - as the rest of these tests run against a real PostgreSQL. What the directory store
/// could not do is the point: two instances see the same images, and the orphan report is the same from either.
/// </summary>
/// <remarks>
/// Each run gets a bucket of its own and removes it. <c>S3_TEST_URL</c> (default <c>http://localhost:8333</c>),
/// <c>SEAWEEDFS_ACCESS_KEY</c> and <c>SEAWEEDFS_SECRET_KEY</c> say where and who.
/// </remarks>
public class S3ProductImageStoreTests : IAsyncLifetime
{
    private static readonly string Url = Environment.GetEnvironmentVariable("S3_TEST_URL") ?? "http://localhost:8333";
    private static readonly string AccessKey = Required("SEAWEEDFS_ACCESS_KEY");
    private static readonly string SecretKey = Required("SEAWEEDFS_SECRET_KEY");

    private readonly string _bucket = $"catalog-tests-{Guid.NewGuid():N}"[..40];
    private readonly List<S3ProductImageStore> _stores = [];
    private readonly List<string> _directories = [];

    public async Task InitializeAsync() => await Instance().EnsureReadyAsync(TimeSpan.FromSeconds(30));

    public async Task DisposeAsync()
    {
        using var s3 = RawClient(SecretKey);
        var listed = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket });
        foreach (var item in listed.S3Objects ?? [])
            await s3.DeleteObjectAsync(_bucket, item.Key);
        await s3.DeleteBucketAsync(_bucket);
        foreach (var store in _stores)
            store.Dispose();
        foreach (var directory in _directories.Where(Directory.Exists))
            Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task What_is_saved_reads_back_and_is_gone_once_deleted()
    {
        var store = Instance();
        var key = Key();

        await store.SaveAsync(key, Png(7));
        var read = await ReadAsync(store, key);
        await store.DeleteAsync(key);
        await store.DeleteAsync(key);   // again: not an error

        Assert.Equal(Png(7), read);
        Assert.Null(await store.OpenReadAsync(key));
        Assert.Null(await store.OpenReadAsync(Key()));
    }

    /// <summary>A key is written once, as the directory store's move is: a second write changes nothing.</summary>
    [Fact]
    public async Task A_key_is_stored_once_and_a_second_write_is_refused()
    {
        var store = Instance();
        var key = Key();
        await store.SaveAsync(key, Png(1));

        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(key, Png(2)));

        Assert.Equal(Png(1), await ReadAsync(store, key));
    }

    [Theory]
    [InlineData("../escape-1.png")]
    [InlineData("0199aa11-1.svg")]
    [InlineData("Upper-1.png")]
    [InlineData("folder/0199aa11-1.png")]
    public async Task A_key_that_is_not_an_image_key_is_refused_before_any_request(string key)
    {
        var store = Instance();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(key, Png(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(key));
    }

    /// <summary>The listing pages through the bucket rather than holding it (specs/033), and skips the store's probe.</summary>
    [Fact]
    public async Task The_listing_pages_through_everything_and_leaves_out_the_stores_own_bookkeeping()
    {
        var store = Instance(pageSize: 2);
        var keys = Enumerable.Range(0, 5).Select(_ => Key()).ToList();
        foreach (var key in keys)
            await store.SaveAsync(key, Png(3));
        using (var s3 = RawClient(SecretKey))
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = _bucket, Key = ".write-probe-left-behind", ContentBody = "" });

        var listed = new List<StoredImage>();
        await foreach (var image in store.ListAsync())
            listed.Add(image);

        Assert.Equal(keys.Order(), listed.Select(i => i.Key).Order());
        Assert.All(listed, i => Assert.Equal(Png(3).Length, i.Size));
        Assert.All(listed, i => Assert.True(DateTimeOffset.UtcNow - i.LastModified < TimeSpan.FromMinutes(5)));
    }

    /// <summary>The issue's acceptance: what one instance stores, another serves - and a deletion reaches both.</summary>
    [Fact]
    public async Task Two_instances_see_each_others_images()
    {
        var first = Instance();
        var second = Instance();
        var key = Key();

        await first.SaveAsync(key, Png(9));
        var readBySecond = await ReadAsync(second, key);
        await second.DeleteAsync(key);

        Assert.Equal(Png(9), readBySecond);
        Assert.Null(await first.OpenReadAsync(key));
        Assert.True(first.SharedAcrossInstances && second.SharedAcrossInstances);
    }

    /// <summary>The issue's acceptance: the orphan report, from either instance, finds nothing a row names.</summary>
    [Fact]
    public async Task The_orphan_report_from_either_instance_finds_only_what_no_row_names()
    {
        var first = Instance();
        var second = Instance();
        var live = new[] { Key(), Key() };
        var orphan = Key();
        await first.SaveAsync(live[0], Png(1));
        await second.SaveAsync(live[1], Png(1));
        await first.SaveAsync(orphan, Png(1));

        foreach (var instance in new[] { first, second })
        {
            var scan = new OrphanImageScan(new LiveKeys(live), instance, Options.Create(new OrphanImageOptions { OrphanGraceHours = 0 }));
            var (orphans, scanned, liveCount, _) = await scan.RunAsync(CancellationToken.None);

            Assert.Equal([orphan], orphans.Select(o => o.Key));
            Assert.Equal((3, 2), (scanned, liveCount));
            Assert.Equal(OrphanImageScan.SharedNote, scan.Note);
        }
    }

    /// <summary>The move off the volume: what the bucket lacks, once - a restart copies nothing twice.</summary>
    [Fact]
    public async Task The_import_copies_what_the_bucket_lacks_and_nothing_twice()
    {
        var bucket = Instance();
        var directory = Directory_();
        var keys = new[] { Key(), Key(), Key() };
        foreach (var key in keys)
            await directory.SaveAsync(key, Png(5));
        await bucket.SaveAsync(keys[0], Png(5));

        var first = await ProductImageImport.RunAsync(directory, bucket);
        var again = await ProductImageImport.RunAsync(directory, bucket);

        Assert.Equal((2, 1, 0), (first.Copied, first.AlreadyThere, first.Failed.Count));
        Assert.Equal((0, 3, 0), (again.Copied, again.AlreadyThere, again.Failed.Count));
        foreach (var key in keys)
            Assert.Equal(Png(5), await ReadAsync(bucket, key));
        Assert.Equal(3, Directory.GetFiles(directory.Root).Length);   // never deletes
    }

    /// <summary>Several instances start at once, each importing: every image arrives once, and nobody fails.</summary>
    [Fact]
    public async Task Imports_running_at_once_copy_each_image_once_and_fail_nothing()
    {
        var directory = Directory_();
        var keys = Enumerable.Range(0, 6).Select(_ => Key()).ToList();
        foreach (var key in keys)
            await directory.SaveAsync(key, Png(4));

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ProductImageImport.RunAsync(directory, Instance())));

        Assert.Equal(keys.Count, results.Sum(r => r.Copied));
        Assert.All(results, r => Assert.Empty(r.Failed));
        Assert.All(results, r => Assert.Equal(keys.Count, r.Copied + r.AlreadyThere));
    }

    /// <summary>A wrong secret or a missing setting stops the service at startup, not at the first upload.</summary>
    [Fact]
    public async Task A_store_that_cannot_write_refuses_to_start_and_says_why()
    {
        var wrong = new S3ProductImageStore(Settings(secret: "not-the-secret"));
        _stores.Add(wrong);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => wrong.EnsureReadyAsync(TimeSpan.Zero));
        var unset = Assert.Throws<InvalidOperationException>(() => new S3ProductImageStore(new S3ImageStoreOptions { ServiceUrl = Url }));

        Assert.Contains(_bucket, refused.Message);
        Assert.Contains("ProductImages:S3:Bucket is not set.", unset.Message);
        Assert.Contains("ProductImages:S3:SecretKey is not set.", unset.Message);
    }

    // ------------------------------------------------------------------ helpers

    private S3ProductImageStore Instance(int pageSize = 1000)
    {
        var store = new S3ProductImageStore(Settings(pageSize: pageSize));
        _stores.Add(store);
        return store;
    }

    private S3ImageStoreOptions Settings(string? secret = null, int pageSize = 1000) => new()
    {
        ServiceUrl = Url,
        Bucket = _bucket,
        AccessKey = AccessKey,
        SecretKey = secret ?? SecretKey,
        PageSize = pageSize,
    };

    private FileSystemProductImageStore Directory_()
    {
        var root = Path.Combine(Path.GetTempPath(), $"catalog_import_{Guid.NewGuid():N}");
        _directories.Add(root);
        return new FileSystemProductImageStore(root);
    }

    private AmazonS3Client RawClient(string secret) => new(
        new BasicAWSCredentials(AccessKey, secret),
        new AmazonS3Config { ServiceURL = Url, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });

    private static string Key() => $"{Guid.NewGuid():N}-{DateTime.UtcNow.Ticks}.png";

    private static byte[] Png(byte marker) => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, marker];

    private static async Task<byte[]> ReadAsync(IProductImageStore store, string key)
    {
        await using var stream = await store.OpenReadAsync(key) ?? throw new InvalidOperationException($"{key} is not there.");
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is not set: the S3 store's tests sign their requests with it (see server/.env).");

    private sealed class LiveKeys(IEnumerable<string> keys) : ILiveImageKeys
    {
        private readonly HashSet<string> _keys = [.. keys];

        public Task<HashSet<string>> GetLiveImageKeysAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_keys);
    }
}
