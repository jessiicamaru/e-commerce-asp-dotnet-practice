using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Finding images no row can name, and — far more importantly — never naming one that a row can
/// (specs/033, issue #67).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>This is the first thing in this project that can destroy data somebody is using.</b>
/// Everything else fails by refusing, leaking, or showing a wrong number. So every dangerous case
/// here was written and seen <b>red</b> before the guard that makes it pass existed: a live
/// product's image, a live variant's image, a file written seconds ago, and a catalogue read that
/// fails.
/// </para>
/// <para>
/// The last of those is the worst. An empty live set makes every file in the store a candidate, and
/// the reclaim removes the whole catalogue's images.
/// </para>
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class OrphanImageTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];

    public void Dispose()
    {
        _fixture.Images.FailDeletes = false;
        GC.SuppressFinalize(this);
    }

    // ---- the four that must never pass by accident -----------------------------------------

    [Fact]
    public async Task A_live_products_image_is_never_an_orphan()
    {
        var product = await AProductAsync();
        await UploadAsync(product.Id, Png);
        var key = await KeyOfProductAsync(product.Id);

        var report = await SendAsync(new FindOrphanImagesQuery());

        Assert.DoesNotContain(report.Orphans, o => o.Key == key);
    }

    [Fact]
    public async Task A_live_variants_image_is_never_an_orphan()
    {
        var product = await AProductAsync();
        var variant = await SendAsync(new AddProductVariantCommand(
            product.Id, $"ORPH-{Guid.NewGuid():N}"[..20], 1_200_000m,
            [new VariantOptionInput("Colour", "Silver")]));
        await SendAsync(new UploadVariantImageCommand(
            product.Id, variant.Id, new MemoryStream(Jpeg), Jpeg.Length));

        var report = await SendAsync(new FindOrphanImagesQuery());

        Assert.DoesNotContain(report.Orphans, o => o.Key.StartsWith($"variant-{variant.Id:N}-"));
    }

    /// <summary>
    /// An upload writes the bytes and THEN switches the row. In between, the file exists and no row
    /// names it — indistinguishable from an orphan, and deleting it destroys an image about to
    /// become live.
    /// </summary>
    [Fact]
    public async Task A_file_written_moments_ago_is_never_an_orphan()
    {
        var key = $"{Guid.CreateVersion7():N}-{DateTime.UtcNow.Ticks}.png";
        await _fixture.Images.SaveAsync(key, Png);      // bytes only: no row will ever name it

        var report = await SendAsync(new FindOrphanImagesQuery());

        Assert.DoesNotContain(report.Orphans, o => o.Key == key);
    }

    /// <summary>
    /// The worst defect available here: an empty live set makes EVERY file a candidate.
    /// </summary>
    [Fact]
    public async Task A_catalogue_that_cannot_be_read_reports_nothing_rather_than_everything()
    {
        var product = await AProductAsync();
        await UploadAsync(product.Id, Png);

        await using var scope = _fixture.NewScope();
        var scan = new OrphanImageScan(
            new FailingProductReads(),
            scope.ServiceProvider.GetRequiredService<IProductImageStore>(),
            Options.Create(new OrphanImageOptions { OrphanGraceHours = 0 }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scan.RunAsync(default));
    }

    // ---- and then what it is actually for ---------------------------------------------------

    [Fact]
    public async Task An_orphan_is_found_and_can_be_reclaimed()
    {
        var orphan = $"{Guid.CreateVersion7():N}-{DateTime.UtcNow.AddDays(-3).Ticks}.png";
        await _fixture.Images.SaveAsync(orphan, Png);
        Aged(orphan, TimeSpan.FromDays(3));

        var live = await AProductAsync();
        await UploadAsync(live.Id, Jpeg);
        var liveKey = await KeyOfProductAsync(live.Id);

        var report = await SendAsync(new FindOrphanImagesQuery());
        Assert.Contains(report.Orphans, o => o.Key == orphan);
        Assert.DoesNotContain(report.Orphans, o => o.Key == liveKey);
        Assert.True(report.LiveKeys > 0, "a report with no live keys is the dangerous case, not a normal one");

        var reclaimed = await SendAsync(new RemoveOrphanImagesCommand());

        Assert.Contains(reclaimed.Orphans, o => o.Key == orphan);
        Assert.DoesNotContain(_fixture.Images.Files(), f => f == orphan);
        Assert.Contains(_fixture.Images.Files(), f => f == liveKey);
    }

    [Fact]
    public async Task A_store_that_refuses_one_key_does_not_stop_the_rest()
    {
        var orphan = $"{Guid.CreateVersion7():N}-{DateTime.UtcNow.AddDays(-3).Ticks}.png";
        await _fixture.Images.SaveAsync(orphan, Png);
        Aged(orphan, TimeSpan.FromDays(3));

        _fixture.Images.FailDeletes = true;

        var reclaimed = await SendAsync(new RemoveOrphanImagesCommand());

        Assert.Contains(orphan, reclaimed.Failed);
        Assert.DoesNotContain(reclaimed.Orphans, o => o.Key == orphan);
    }

    [Fact]
    public async Task The_stores_own_bookkeeping_is_not_waste()
    {
        var probe = Path.Combine(_fixture.Images.Inner.Root, $".write-probe-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(probe, []);
        File.SetLastWriteTimeUtc(probe, DateTime.UtcNow.AddDays(-3));

        var report = await SendAsync(new FindOrphanImagesQuery());

        Assert.DoesNotContain(report.Orphans, o => o.Key.StartsWith('.'));
        File.Delete(probe);
    }

    // ---- helpers ----------------------------------------------------------------------------

    /// <summary>Back-dates a file so the grace period does not hide it from a test about orphans.</summary>
    private void Aged(string key, TimeSpan by) =>
        File.SetLastWriteTimeUtc(Path.Combine(_fixture.Images.Inner.Root, key), DateTime.UtcNow - by);

    private async Task<string> KeyOfProductAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var product = await products.GetByIdAsync(productId);
        return ProductImageKey.For(product!)!;
    }

    private Task UploadAsync(Guid productId, byte[] content) =>
        SendAsync(new Ecommerce.Catalog.Application.Products.Images.UploadProductImage.UploadProductImageCommand(
            productId, new MemoryStream(content), content.Length));

    private async Task<Ecommerce.Catalog.Application.Products.Common.ProductResponse> AProductAsync()
    {
        await using var scope = _fixture.NewScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var categories = await sender.Send(
            new Ecommerce.Catalog.Application.Categories.Queries.GetCategories.GetCategoriesQuery());

        var categoryId = categories.Count > 0
            ? categories[0].Id
            : (await sender.Send(new Ecommerce.Catalog.Application.Categories.Commands.CreateCategory.CreateCategoryCommand(
                $"Orph {Guid.NewGuid():N}"[..20], null, $"orph-{Guid.NewGuid():N}"[..20], null))).Id;

        return await sender.Send(new CreateProductCommand(
            $"Orphan camera {Guid.NewGuid():N}"[..26], null, 1_000_000m,
            $"ORP-{Guid.NewGuid():N}"[..16], categoryId));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>
    /// A catalogue read that fails. One method, because the scan needs one - which is why
    /// <c>ILiveImageKeys</c> exists rather than the scan taking the whole repository.
    /// </summary>
    private sealed class FailingProductReads : ILiveImageKeys
    {
        public Task<HashSet<string>> GetLiveImageKeysAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The catalogue could not be read.");
    }
}
