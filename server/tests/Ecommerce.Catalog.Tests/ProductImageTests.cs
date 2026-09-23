using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
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

    /// <summary>
    /// A deleted product takes its picture with it (specs/029, issue #66).
    /// </summary>
    /// <remarks>
    /// Until this was written, <c>DeleteProductCommandHandler</c> never asked for the store: the row
    /// went and the bytes stayed on the volume forever, unreachable, with nothing to reclaim them.
    /// Nothing broke, which is why it went unnoticed - two orphans were found by listing the
    /// directory while answering a question about where images are kept.
    /// </remarks>
    [Fact]
    public async Task Deleting_a_product_deletes_its_image()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);
        Assert.Single(FilesOf(productId));

        await SendAsync(new DeleteProductCommand(productId));

        Assert.Empty(FilesOf(productId));
    }

    /// <summary>
    /// The store failing must not stop the product going.
    /// </summary>
    /// <remarks>
    /// <c>DELETE /api/products/{id}</c> exists for rows that should never have existed (specs/024).
    /// A product that cannot be removed from the catalogue because of a leftover PNG is a worse
    /// defect than the leak this feature fixes, and a read-only volume cannot be retried into
    /// success. The file is left behind and said so in the log - the same bargain
    /// <c>RemoveProductImage</c> already strikes.
    /// </remarks>
    [Fact]
    public async Task A_failing_store_does_not_stop_a_product_being_deleted()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);

        _fixture.Images.FailDeletes = true;

        await SendAsync(new DeleteProductCommand(productId));   // does not throw

        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Assert.Null(await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId));

        // ...and the orphan it leaves is the deliberate, bounded cost of that choice.
        _fixture.Images.FailDeletes = false;
        Assert.Single(FilesOf(productId));
    }

    /// <summary>
    /// A shape with its own photograph shows it; one without shows the product's (specs/032).
    /// </summary>
    /// <remarks>
    /// The fallback is resolved on the server, in <c>VariantResponse</c>, so it is decided once.
    /// Two callers implementing it separately would be two chances to get it wrong, and the one
    /// that got it wrong would show the previously chosen variant's picture - which looks exactly
    /// like the feature working.
    /// </remarks>
    [Fact]
    public async Task A_variant_without_its_own_photograph_falls_back_to_the_products()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);

        // TWO shapes: SeedProductAsync creates none, so one would leave nothing to fall back with.
        var photographed = await SendAsync(new AddProductVariantCommand(
            productId, $"FB1-{Guid.NewGuid():N}"[..20], 1_500_000m,
            [new VariantOptionInput("Colour", "Silver")]));
        var bare = await SendAsync(new AddProductVariantCommand(
            productId, $"FB2-{Guid.NewGuid():N}"[..20], 1_600_000m,
            [new VariantOptionInput("Colour", "Black")]));
        await UploadVariantAsync(productId, photographed.Id, Jpeg);

        // GetProductByIdQuery, not ProductResponse.From: only the lookup fills Variants.
        var product = await SendAsync(new GetProductByIdQuery(productId));
        var variants = product.Variants!;

        var withOwn = variants.Single(v => v.Id == photographed.Id);
        var without = variants.Single(v => v.Id == bare.Id);

        Assert.Contains($"/variants/{photographed.Id}/image", withOwn.ImageUrl);
        Assert.Equal($"/api/products/{productId}/image", without.ImageUrl!.Split('?')[0]);
    }

    /// <summary>Neither the shape nor the product has one: null, not a made-up address.</summary>
    [Fact]
    public async Task A_variant_of_a_product_with_no_photograph_at_all_has_none()
    {
        var productId = await SeedProductAsync();
        await SendAsync(new AddProductVariantCommand(
            productId, $"NONE-{Guid.NewGuid():N}"[..20], 1_500_000m,
            [new VariantOptionInput("Colour", "Black")]));

        var product = await SendAsync(new GetProductByIdQuery(productId));

        Assert.All(product.Variants!, v => Assert.Null(v.ImageUrl));
    }

    /// <summary>
    /// Replacing a shape's photograph leaves exactly one file, like replacing a product's.
    /// </summary>
    [Fact]
    public async Task Replacing_a_variants_photograph_leaves_one_file()
    {
        var productId = await SeedProductAsync();
        var second = await SendAsync(new AddProductVariantCommand(
            productId, $"RP-{Guid.NewGuid():N}"[..20], 1_500_000m,
            [new VariantOptionInput("Colour", "Silver")]));

        await UploadVariantAsync(productId, second.Id, Png);
        await UploadVariantAsync(productId, second.Id, Jpeg);
        await UploadVariantAsync(productId, second.Id, WebP);

        Assert.Single(_fixture.Images.Files().Where(f => f.StartsWith($"variant-{second.Id:N}-")));
    }

    /// <summary>The bytes decide the type here too - an SVG renamed .png is refused.</summary>
    [Fact]
    public async Task A_variant_photograph_is_recognised_by_its_bytes()
    {
        var productId = await SeedProductAsync();
        var variant = await SendAsync(new AddProductVariantCommand(
            productId, $"SVG-{Guid.NewGuid():N}"[..20], 1_500_000m,
            [new VariantOptionInput("Colour", "Black")]));
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray();

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => UploadVariantAsync(productId, variant.Id, svg));

        Assert.Contains("not a JPEG, PNG or WebP", error.Message);
    }

    /// <summary>
    /// A deleted product takes EVERY shape's photograph with it (specs/032).
    /// </summary>
    /// <remarks>
    /// specs/029 closed exactly this leak for the product's own image. Variant images added without
    /// extending that cleanup would reintroduce the same defect in the same month - the row goes,
    /// the bytes stay on the volume forever, and nothing breaks so nobody notices.
    /// </remarks>
    [Fact]
    public async Task Deleting_a_product_deletes_every_variants_image_too()
    {
        var productId = await SeedProductAsync();
        await UploadAsync(productId, Png);

        var second = await SendAsync(new AddProductVariantCommand(
            productId, $"SECOND-{Guid.NewGuid():N}"[..20], 1_500_000m,
            [new VariantOptionInput("Colour", "Silver")]));
        await UploadVariantAsync(productId, second.Id, Jpeg);

        Assert.Equal(2, _fixture.Images.Files().Count(f =>
            f.Contains($"{productId:N}") || f.Contains($"{second.Id:N}")));

        await SendAsync(new DeleteProductCommand(productId));

        Assert.DoesNotContain(_fixture.Images.Files(), f =>
            f.Contains($"{productId:N}") || f.Contains($"{second.Id:N}"));
    }

    /// <summary>
    /// A product and its first variant must not name the same file (specs/032).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>The first variant of a product REUSES the product's id</b> (specs/020) - 12 of 12
    /// products in the live catalogue. Without the prefix, the two keys differ only by their two
    /// <c>ImageUpdatedAt</c> values happening not to agree, so this test pins the ONE case where
    /// they do agree: the same id and the same instant. That is the case a clock cannot save.
    /// </remarks>
    [Fact]
    public void A_product_and_its_first_variant_never_name_the_same_file()
    {
        var shared = Guid.CreateVersion7();
        var sameInstant = ProductImageKey.Truncate(DateTime.UtcNow);
        var png = ImageFormat.FromContentType("image/png")!;

        var productKey = ProductImageKey.For(shared, sameInstant, png);
        var variantKey = ProductImageKey.ForVariant(shared, sameInstant, png);

        Assert.NotEqual(productKey, variantKey);
    }

    private Task UploadVariantAsync(Guid productId, Guid variantId, byte[] content) =>
        SendAsync(new UploadVariantImageCommand(
            productId, variantId, new MemoryStream(content), content.Length));

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
