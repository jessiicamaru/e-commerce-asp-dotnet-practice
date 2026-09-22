using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using Ecommerce.Catalog.Application.Products.Images.RemoveProductImage;
using Ecommerce.Catalog.Application.Products.Images.UploadProductImage;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A product's image (specs/019): stored outside the database, recognised by its bytes, and replaced
/// so that the product never points at a file that does not exist.
/// </summary>
/// <remarks>
/// The real filesystem store and a real PostgreSQL. The switch is a guarded UPDATE, which only a
/// database evaluates, and the ordering is about files that really exist or do not.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class ProductImageTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];
    private static readonly byte[] WebP = [.. "RIFF"u8, 20, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8];

    public void Dispose()
    {
        _fixture.Images.FailWrites = false;
        _fixture.Images.FailDeletes = false;
        _fixture.Images.AfterSave = null;
    }

    [Fact]
    public async Task An_upload_sets_the_row_and_stores_the_bytes()
    {
        var productId = await SeedProductAsync();

        var response = await UploadAsync(productId, Png);

        var row = await ReadAsync(productId);
        Assert.Equal("image/png", row.ImageContentType);
        Assert.Equal($"/api/products/{productId}/image?v={ProductImageKey.Version(row.ImageUpdatedAt!.Value)}", response.ImageUrl);

        var image = await SendAsync(new GetProductImageQuery(productId));
        Assert.NotNull(image);
        await using (image.Content)
        {
            Assert.Equal("image/png", image.ContentType);
            Assert.Equal(Png, await ReadAllAsync(image.Content));
        }
    }

    [Fact]
    public async Task The_listing_and_the_lookup_carry_the_same_address()
    {
        var productId = await SeedProductAsync();
        var uploaded = await UploadAsync(productId, Jpeg);

        var lookedUp = await ReadResponseAsync(productId);

        Assert.Equal(uploaded.ImageUrl, lookedUp.ImageUrl);
        Assert.Equal("image/jpeg", (await ReadAsync(productId)).ImageContentType);
    }

    [Fact]
    public async Task Replacing_switches_to_the_new_file_and_deletes_the_old_one()
    {
        var productId = await SeedProductAsync();
        var first = await UploadAsync(productId, Png);

        var second = await UploadAsync(productId, WebP);

        Assert.NotEqual(first.ImageUrl, second.ImageUrl);
        Assert.Equal("image/webp", (await ReadAsync(productId)).ImageContentType);
        Assert.Equal([$"{productId:N}-"], FilesOf(productId).Select(f => f[..33]));   // exactly one file
        Assert.EndsWith(".webp", Assert.Single(FilesOf(productId)));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("svg")]
    [InlineData("gif")]
    public async Task The_bytes_decide_the_type_and_anything_else_is_refused(string kind)
    {
        var productId = await SeedProductAsync();
        byte[] content = kind switch
        {
            "text" => "this is not an image, whatever the header says"u8.ToArray(),
            "svg" => "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray(),
            _ => "GIF89a...."u8.ToArray(),
        };

        var refused = await Assert.ThrowsAsync<ValidationException>(() => UploadAsync(productId, content));

        Assert.Equal("File", Assert.Single(refused.Errors).PropertyName);
        Assert.Null((await ReadAsync(productId)).ImageContentType);
        Assert.Empty(FilesOf(productId));
    }

    [Fact]
    public async Task Too_big_empty_or_lying_about_its_length_is_refused_and_the_old_image_stays()
    {
        var productId = await SeedProductAsync();
        var kept = await UploadAsync(productId, Png);
        var tooBig = new byte[ProductImageKey.MaxBytes + 1];
        Png.CopyTo(tooBig, 0);

        await Assert.ThrowsAsync<ValidationException>(() => UploadAsync(productId, tooBig));
        await Assert.ThrowsAsync<ValidationException>(() => UploadAsync(productId, []));
        // Declares 13 bytes, sends 2 MB + 1: the length is a claim too, so the reading is bounded.
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new UploadProductImageCommand(productId, new MemoryStream(tooBig), Png.Length)));

        Assert.Equal(kept.ImageUrl, (await ReadResponseAsync(productId)).ImageUrl);
        Assert.Single(FilesOf(productId));
    }

    [Fact]
    public async Task A_write_that_fails_leaves_the_product_on_its_previous_image()
    {
        var productId = await SeedProductAsync();
        var kept = await UploadAsync(productId, Png);
        _fixture.Images.FailWrites = true;

        await Assert.ThrowsAsync<IOException>(() => UploadAsync(productId, WebP));

        _fixture.Images.FailWrites = false;
        Assert.Equal(kept.ImageUrl, (await ReadResponseAsync(productId)).ImageUrl);
        Assert.NotNull(await SendAsync(new GetProductImageQuery(productId)));   // still resolves
    }

    [Fact]
    public async Task A_delete_that_fails_after_the_switch_is_waste_not_an_error()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);
        _fixture.Images.FailDeletes = true;

        var replaced = await UploadAsync(productId, WebP);

        Assert.Equal(replaced.ImageUrl, (await ReadResponseAsync(productId)).ImageUrl);
        Assert.Equal(2, FilesOf(productId).Count);   // the old one is an orphan, and the product is right
    }

    [Fact]
    public async Task A_replacement_that_loses_a_race_is_409_and_leaves_no_file_behind()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);

        // Another administrator replaces the image after this upload has read the row and written its
        // file, but before it switches the row.
        _fixture.Images.AfterSave = async () =>
        {
            await using var scope = _fixture.NewScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var current = (await ReadAsync(productId)).ImageUpdatedAt;
            Assert.Equal(1, await repository.TrySetImageAsync(
                productId, current, "image/png", ProductImageKey.Truncate(DateTime.UtcNow.AddMinutes(1))));
        };

        await Assert.ThrowsAsync<ConflictException>(() => UploadAsync(productId, WebP));

        Assert.DoesNotContain(FilesOf(productId), f => f.EndsWith(".webp"));
    }

    [Fact]
    public async Task Removing_clears_the_row_and_the_file_and_removing_again_is_quiet()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);

        await SendAsync(new RemoveProductImageCommand(productId));
        await SendAsync(new RemoveProductImageCommand(productId));

        var row = await ReadAsync(productId);
        Assert.Null(row.ImageContentType);
        Assert.Null(row.ImageUpdatedAt);
        Assert.Null((await ReadResponseAsync(productId)).ImageUrl);
        Assert.Empty(FilesOf(productId));
        Assert.Null(await SendAsync(new GetProductImageQuery(productId)));
    }

    [Fact]
    public async Task An_unknown_product_is_not_found()
    {
        var nobody = Guid.CreateVersion7();

        await Assert.ThrowsAsync<NotFoundException>(() => UploadAsync(nobody, Png));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new RemoveProductImageCommand(nobody)));
        Assert.Null(await SendAsync(new GetProductImageQuery(nobody)));
    }

    [Fact]
    public async Task The_database_refuses_half_an_image_and_an_unknown_type()
    {
        var productId = await SeedProductAsync();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var halfAnImage = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE products SET \"ImageContentType\" = 'image/png' WHERE \"Id\" = {productId}"));
        Assert.Equal("CK_products_image_complete", halfAnImage.ConstraintName);

        var svg = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE products SET \"ImageContentType\" = 'image/svg+xml', \"ImageUpdatedAt\" = now() WHERE \"Id\" = {productId}"));
        Assert.Equal("CK_products_image_type", svg.ConstraintName);
    }

    private Task<ProductResponse> UploadAsync(Guid productId, byte[] content) =>
        SendAsync(new UploadProductImageCommand(productId, new MemoryStream(content), content.Length));

    private List<string> FilesOf(Guid productId) =>
        _fixture.Images.Files().Where(f => f.StartsWith($"{productId:N}-")).ToList();

    private async Task<Product> ReadAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    private async Task<ProductResponse> ReadResponseAsync(Guid productId) =>
        ProductResponse.From(await ReadAsync(productId));

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

    private async Task<Guid> SeedProductAsync()
    {
        var categoryId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        context.Categories.Add(new Category
        {
            Id = categoryId,
            Name = $"Img {categoryId:N}"[..20],
            Slug = $"img-{categoryId:N}"[..20]
        });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = $"Img {productId:N}"[..20],
            Price = 9.99m,
            Sku = $"IMG{productId:N}"[..20],
            CategoryId = categoryId
        });

        await context.SaveChangesAsync();
        return productId;
    }
}
