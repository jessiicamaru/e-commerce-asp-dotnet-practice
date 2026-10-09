using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using Ecommerce.Catalog.Application.Products.Images.RemoveProductImage;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A gallery of photographs per product (specs/160, #368): the cover stays the product's own photograph, the rest are
/// rows in <c>product_photos</c> - on a real PostgreSQL and the real filesystem store, because the rules are about
/// guarded statements and files that really exist or do not.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ProductGalleryTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    // Each photograph different, so a test can tell which one it is reading.
    private static byte[] Png(byte n) => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, n, 2, 3, 4, 5];

    public void Dispose()
    {
        _fixture.Images.AfterSave = null;
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
    }

    [Fact]
    public async Task The_first_photograph_is_the_cover_and_the_rest_follow_in_order()
    {
        var productId = await SeedProductAsync();

        var first = await AddAsync(productId, Png(1));
        Assert.NotNull(first.ImageUrl);
        Assert.Empty(first.Photos!);

        await AddAsync(productId, Png(2));
        var third = await AddAsync(productId, Png(3));

        // The lookup carries them after the cover, in the order added, each with its own address.
        var lookedUp = (await SendAsync(new GetProductByIdQuery(productId)))!;
        Assert.Equal(first.ImageUrl, lookedUp.ImageUrl);
        Assert.Equal(third.Photos!.Select(p => p.Id), lookedUp.Photos!.Select(p => p.Id));
        Assert.Equal([Png(2), Png(3)], await BytesOfAsync(productId, lookedUp.Photos!));
        Assert.All(lookedUp.Photos!, p => Assert.StartsWith($"/api/products/{productId}/photos/{p.Id}?v=", p.Url));
        Assert.Equal(2, PhotoFiles().Count(f => lookedUp.Photos!.Any(p => f.Contains($"{p.Id:N}"))));
    }

    [Fact]
    public async Task A_product_holds_ten_photographs_and_no_more()
    {
        var productId = await SeedProductAsync();
        for (byte n = 1; n <= ProductGallery.MaxPhotos; n++)
        {
            await AddAsync(productId, Png(n));
        }

        var refused = await Assert.ThrowsAsync<ConflictException>(() => AddAsync(productId, Png(11)));

        Assert.Contains("10", refused.Message);
        Assert.Equal(ProductGallery.MaxPhotos - 1, (await PhotosAsync(productId)).Count);
    }

    [Fact]
    public async Task A_reorder_names_every_photograph_once_or_changes_nothing()
    {
        var productId = await SeedProductAsync();
        await AddAsync(productId, Png(1));
        await AddAsync(productId, Png(2));
        await AddAsync(productId, Png(3));
        var before = (await PhotosAsync(productId)).Select(p => p.Id).ToList();
        var other = await SeedProductAsync();
        await AddAsync(other, Png(7));
        var stranger = (await AddAsync(other, Png(8))).Photos!.Single().Id;

        var reordered = await SendAsync(new ReorderProductPhotosCommand(productId, [before[1], before[0]]));
        Assert.Equal([before[1], before[0]], reordered.Photos!.Select(p => p.Id));

        // One missing, a stranger's, the same one twice: each refused, the order as it was.
        var now = (await PhotosAsync(productId)).Select(p => p.Id).ToList();
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new ReorderProductPhotosCommand(productId, [now[0]])));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new ReorderProductPhotosCommand(productId, [now[0], stranger])));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new ReorderProductPhotosCommand(productId, [now[0], now[0]])));
        Assert.Equal(now, (await PhotosAsync(productId)).Select(p => p.Id));
    }

    [Fact]
    public async Task Making_a_photograph_the_cover_puts_the_old_cover_in_its_place_and_leaves_no_file_behind()
    {
        var productId = await SeedProductAsync();
        var cover = await AddAsync(productId, Png(1));
        await AddAsync(productId, Png(2));
        var withThree = await AddAsync(productId, Png(3));
        var chosen = withThree.Photos![1];

        var after = await SendAsync(new MakeProductPhotoCoverCommand(productId, chosen.Id));

        // The chosen photograph is the cover - a new address, its bytes - and the old cover sits where it was.
        Assert.NotEqual(cover.ImageUrl, after.ImageUrl);
        Assert.Equal(Png(3), await CoverBytesAsync(productId));
        Assert.Equal(2, after.Photos!.Count);
        Assert.Equal(withThree.Photos[0].Id, after.Photos[0].Id);
        Assert.Equal([Png(2), Png(1)], await BytesOfAsync(productId, after.Photos));

        // Its old address opens nothing, and every file of this product is one a row names.
        Assert.Null(await SendAsync(new GetProductPhotoQuery(productId, chosen.Id)));
        await AssertNoOrphansAsync(productId, after.Photos.Select(p => p.Id));
    }

    [Fact]
    public async Task Removing_the_cover_promotes_the_first_photograph_and_removing_a_photograph_removes_just_it()
    {
        var productId = await SeedProductAsync();
        await AddAsync(productId, Png(1));
        await AddAsync(productId, Png(2));
        var three = await AddAsync(productId, Png(3));

        await SendAsync(new RemoveProductImageCommand(productId));

        Assert.Equal(Png(2), await CoverBytesAsync(productId));
        var left = await PhotosAsync(productId);
        Assert.Equal([three.Photos![1].Id], left.Select(p => p.Id));

        await SendAsync(new RemoveProductPhotoCommand(productId, left[0].Id));

        Assert.Empty(await PhotosAsync(productId));
        Assert.Equal(Png(2), await CoverBytesAsync(productId));
        await AssertNoOrphansAsync(productId, []);
    }

    [Fact]
    public async Task A_sellers_change_sends_an_approved_product_back_to_review_and_staff_changes_do_not()
    {
        var seller = Guid.CreateVersion7();
        var productId = await SeedProductAsync(seller);
        await AddAsync(productId, Png(1));   // staff: the cover
        Assert.Equal(ProductReviewStatus.Approved, (await ReadAsync(productId)).ReviewStatus);

        As(seller, "Seller");
        await AddAsync(productId, Png(2));

        Assert.Equal(ProductReviewStatus.Pending, (await ReadAsync(productId)).ReviewStatus);
    }

    [Fact]
    public async Task Another_sellers_product_is_not_found_for_every_change()
    {
        var productId = await SeedProductAsync(Guid.CreateVersion7());
        await AddAsync(productId, Png(1));
        var photo = (await AddAsync(productId, Png(2))).Photos!.Single().Id;

        As(Guid.CreateVersion7(), "Seller");

        await Assert.ThrowsAsync<NotFoundException>(() => AddAsync(productId, Png(3)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new RemoveProductPhotoCommand(productId, photo)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new MakeProductPhotoCoverCommand(productId, photo)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new ReorderProductPhotosCommand(productId, [photo])));
    }

    [Fact]
    public async Task Off_the_shelf_a_photograph_is_served_only_with_its_own_key()
    {
        var productId = await SeedProductAsync(Guid.CreateVersion7(), ProductReviewStatus.Pending);
        await AddAsync(productId, Png(1));
        await AddAsync(productId, Png(2));
        var photo = Assert.Single(await PhotosAsync(productId));

        Assert.Null(await SendAsync(new GetProductPhotoQuery(productId, photo.Id)));
        Assert.Null(await SendAsync(new GetProductPhotoQuery(productId, photo.Id, Guid.NewGuid())));
        var served = await SendAsync(new GetProductPhotoQuery(productId, photo.Id, photo.AccessKey));
        Assert.NotNull(served);
        await served.Content.DisposeAsync();
        Assert.False(served.Public);
    }

    [Fact]
    public async Task A_concurrent_cover_change_loses_cleanly()
    {
        var productId = await SeedProductAsync();
        await AddAsync(productId, Png(1));
        var photo = (await AddAsync(productId, Png(2))).Photos!.Single().Id;
        var filesBefore = _fixture.Images.Files().Count;

        // Somebody else switches the cover between this change writing its bytes and switching the row.
        _fixture.Images.AfterSave = async () =>
        {
            await using var scope = _fixture.NewScope();
            var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var row = await ReadAsync(productId);
            Assert.Equal(1, await products.TrySetImageAsync(
                productId, row.ImageUpdatedAt, row.ImageContentType, row.ImageUpdatedAt!.Value.AddTicks(20), Guid.NewGuid()));
        };

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new MakeProductPhotoCoverCommand(productId, photo)));

        // Nothing changed in the gallery, and the bytes it wrote were taken back.
        Assert.Equal(photo, Assert.Single(await PhotosAsync(productId)).Id);
        Assert.Equal(filesBefore, _fixture.Images.Files().Count);
    }

    [Fact]
    public async Task Deleting_the_product_deletes_every_photographs_file()
    {
        var productId = await SeedProductAsync();
        await AddAsync(productId, Png(1));
        var photos = (await AddAsync(productId, Png(2))).Photos!.Select(p => p.Id).ToList();

        await SendAsync(new DeleteProductCommand(productId));

        Assert.DoesNotContain(_fixture.Images.Files(), f => f.StartsWith($"{productId:N}-") || photos.Any(p => f.Contains($"{p:N}")));
    }

    [Fact]
    public async Task The_review_queue_shows_every_photograph()
    {
        var productId = await SeedProductAsync(Guid.CreateVersion7(), ProductReviewStatus.Pending);
        await AddAsync(productId, Png(1));
        await AddAsync(productId, Png(2));
        await AddAsync(productId, Png(3));

        var queue = await SendAsync(new GetReviewQueueQuery("Pending", 1, 50));

        var row = Assert.Single(queue.Items, p => p.Id == productId);
        Assert.NotNull(row.ImageUrl);
        Assert.Equal(2, row.Photos!.Count);
    }

    /// <summary>Every file of this product is one a row names, and there is exactly one per row: nothing left behind.</summary>
    private async Task AssertNoOrphansAsync(Guid productId, IEnumerable<Guid> photoIds)
    {
        await using var scope = _fixture.NewScope();
        var live = await scope.ServiceProvider.GetRequiredService<IProductRepository>().GetLiveImageKeysAsync();
        var rows = await PhotosAsync(productId);
        // A former cover in the gallery keeps the cover's key form (research D2), so its file starts with the product id.
        var ours = _fixture.Images.Files()
            .Where(f => f.StartsWith($"{productId:N}-") || photoIds.Any(p => f.Contains($"{p:N}")) || rows.Any(r => r.StorageKey == f))
            .ToList();
        Assert.All(ours, f => Assert.Contains(f, live));
        Assert.Equal(1 + rows.Count, ours.Count);
    }

    private IEnumerable<string> PhotoFiles() => _fixture.Images.Files().Where(f => f.StartsWith("photo-"));

    private async Task<byte[]> CoverBytesAsync(Guid productId)
    {
        var image = await SendAsync(new GetProductImageQuery(productId));
        Assert.NotNull(image);
        await using (image.Content)
        {
            return await ReadAllAsync(image.Content);
        }
    }

    private async Task<List<byte[]>> BytesOfAsync(Guid productId, IEnumerable<ProductPhotoResponse> photos)
    {
        var all = new List<byte[]>();
        foreach (var photo in photos)
        {
            var image = await SendAsync(new GetProductPhotoQuery(productId, photo.Id));
            Assert.NotNull(image);
            await using (image.Content)
            {
                all.Add(await ReadAllAsync(image.Content));
            }
        }

        return all;
    }

    private Task<ProductResponse> AddAsync(Guid productId, byte[] content) =>
        SendAsync(new AddProductPhotoCommand(productId, new MemoryStream(content), content.Length));

    private async Task<List<ProductPhoto>> PhotosAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.ProductPhotos.AsNoTracking()
            .Where(p => p.ProductId == productId).OrderBy(p => p.Position).ThenBy(p => p.CreatedAt).ToListAsync();
    }

    private async Task<Product> ReadAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
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

    private async Task<Guid> SeedProductAsync(Guid? sellerId = null, ProductReviewStatus status = ProductReviewStatus.Approved)
    {
        var categoryId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        context.Categories.Add(new Category
        {
            Id = categoryId,
            Name = $"Gal {categoryId:N}"[..20],
            Slug = $"gal-{categoryId:N}"[..20],
        });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = $"Gal {productId:N}"[..20],
            Price = 9.99m,
            Sku = $"GAL{productId:N}"[..20],
            CategoryId = categoryId,
            SellerId = sellerId,
            ReviewStatus = status,
            // Oldest first in the moderators' queue, so a test finds its own on the first page.
            SubmittedAt = status == ProductReviewStatus.Pending ? new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
        });

        await context.SaveChangesAsync();
        return productId;
    }
}
