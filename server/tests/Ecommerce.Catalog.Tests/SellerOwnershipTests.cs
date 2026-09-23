using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Queries.GetMyProducts;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A seller may touch their own listings and nothing else (specs/027).
/// </summary>
/// <remarks>
/// <b>Every write is tested separately, on purpose.</b> SC-001 says per operation rather than once,
/// because the check lives in nine handlers: one that forgets to call it is a hole that a single
/// happy-path test would never find, and the ninth is the one a reviewer skips.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class SellerOwnershipTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    /// <summary>
    /// Puts the caller back to an administrator after every test in this class.
    /// </summary>
    /// <remarks>
    /// <b>The caller is a singleton for the whole collection.</b> Without this, the first test here
    /// that becomes a seller leaves every test that runs afterwards acting as that seller - which is
    /// how eleven image tests went red on a change that had nothing to do with images. The same trap
    /// caught the language holder in specs/022; a shared holder is only safe if it is put back.
    /// </remarks>
    public void Dispose()
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
        GC.SuppressFinalize(this);
    }

    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();

    [Fact]
    public async Task A_sellers_product_records_them_and_reads_back_with_their_shop_name()
    {
        await RecordShopAsync(_alice, "Alice Cameras");
        var product = await AsSeller(_alice, CreateProductAsync);

        var read = await AsAdmin(() => SendAsync(new GetProductByIdQuery(product.Id)));

        Assert.Equal(_alice, read!.SellerId);
        Assert.Equal("Alice Cameras", read.SellerName);
    }

    [Fact]
    public async Task An_administrators_product_belongs_to_the_shop_itself()
    {
        var product = await AsAdmin(CreateProductAsync);

        var read = await AsAdmin(() => SendAsync(new GetProductByIdQuery(product.Id)));

        // Null, not an invented house account: the shape every product listed before sellers has.
        Assert.Null(read!.SellerId);
        Assert.Null(read.SellerName);
    }

    // ---------------------------------------------------------------- one per write operation

    [Fact]
    public async Task Another_sellers_product_cannot_be_deleted()
    {
        var product = await AsSeller(_alice, CreateProductAsync);

        await AsSeller(_bob, () => Refused(new DeleteProductCommand(product.Id)));
    }

    [Fact]
    public async Task Another_sellers_product_cannot_gain_a_variant()
    {
        var product = await AsSeller(_alice, CreateProductAsync);

        await AsSeller(_bob, () => Refused(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-X", 1_000_000m, [new VariantOptionInput("Kit", "Body")])));
    }

    [Fact]
    public async Task Another_sellers_variant_cannot_be_repriced()
    {
        var product = await AsSeller(_alice, CreateProductAsync);
        var variant = Assert.Single(product.Variants!);

        await AsSeller(_bob, () => Refused(
            new UpdateProductVariantCommand(product.Id, variant.Id, 1m, IsActive: false)));
    }

    [Fact]
    public async Task Another_sellers_product_cannot_be_translated()
    {
        var product = await AsSeller(_alice, CreateProductAsync);

        await AsSeller(_bob, () => Refused(
            new SetProductTranslationCommand(product.Id, "en", "Stolen", null)));
    }

    [Fact]
    public async Task Another_sellers_translation_cannot_be_removed()
    {
        var product = await AsSeller(_alice, CreateProductAsync);
        await AsSeller(_alice, () => SendAsync(new SetProductTranslationCommand(product.Id, "en", "Mine", null)));

        await AsSeller(_bob, () => Refused(new RemoveProductTranslationCommand(product.Id, "en")));
    }

    [Fact]
    public async Task Another_sellers_option_cannot_be_translated()
    {
        var product = await AsSeller(_alice, () => CreateProductAsync([new VariantOptionInput("Kit", "Body only")]));
        var option = Assert.Single(Assert.Single(product.Variants!).Options);

        await AsSeller(_bob, () => Refused(
            new SetOptionTranslationCommand(product.Id, option.Id, "en", "Kit", "Body")));
    }

    [Fact]
    public async Task Another_sellers_variant_cannot_be_priced_in_another_currency()
    {
        var product = await AsSeller(_alice, CreateProductAsync);
        var variant = Assert.Single(product.Variants!);

        await AsSeller(_bob, () => Refused(
            new SetVariantPriceCommand(product.Id, variant.Id, "USD", 1m)));
    }

    [Fact]
    public async Task Another_sellers_price_cannot_be_removed()
    {
        var product = await AsSeller(_alice, CreateProductAsync);
        var variant = Assert.Single(product.Variants!);
        await AsSeller(_alice, () => SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 999m)));

        await AsSeller(_bob, () => Refused(new RemoveVariantPriceCommand(product.Id, variant.Id, "USD")));
    }

    [Fact]
    public async Task The_shops_own_product_cannot_be_adopted_by_a_seller()
    {
        // A product with no seller belongs to the shop. A seller must not be able to take it over by
        // being the only one who asked.
        var product = await AsAdmin(CreateProductAsync);

        await AsSeller(_alice, () => Refused(new DeleteProductCommand(product.Id)));
    }

    // ---------------------------------------------------------------- the other direction

    [Fact]
    public async Task A_seller_may_do_all_of_it_to_their_own()
    {
        var product = await AsSeller(_alice, () => CreateProductAsync([new VariantOptionInput("Kit", "Body only")]));
        var variant = Assert.Single(product.Variants!);
        var option = Assert.Single(variant.Options);

        await AsSeller(_alice, async () =>
        {
            await SendAsync(new SetProductTranslationCommand(product.Id, "en", "Mine", null));
            await SendAsync(new SetOptionTranslationCommand(product.Id, option.Id, "en", "Kit", "Body only"));
            await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "USD", 1499m));
            await SendAsync(new UpdateProductVariantCommand(product.Id, variant.Id, 41_000_000m, IsActive: true));
            await SendAsync(new RemoveVariantPriceCommand(product.Id, variant.Id, "USD"));
            await SendAsync(new DeleteProductCommand(product.Id));
            return 0;
        });
    }

    [Fact]
    public async Task An_administrator_may_touch_anybodys_listing_because_that_is_moderation()
    {
        var product = await AsSeller(_alice, CreateProductAsync);

        await AsAdmin(async () =>
        {
            await SendAsync(new SetProductTranslationCommand(product.Id, "en", "Moderated", null));
            await SendAsync(new DeleteProductCommand(product.Id));
            return 0;
        });
    }

    [Fact]
    public async Task My_listings_holds_mine_and_only_mine()
    {
        await RecordShopAsync(_alice, "Alice Cameras");
        var mine = await AsSeller(_alice, CreateProductAsync);
        var theirs = await AsSeller(_bob, CreateProductAsync);

        var page = await AsSeller(_alice, () => SendAsync(new GetMyProductsQuery { PageSize = 200 }));

        Assert.Contains(page.Items, p => p.Id == mine.Id);
        Assert.DoesNotContain(page.Items, p => p.Id == theirs.Id);
        Assert.All(page.Items, p => Assert.Equal("Alice Cameras", p.SellerName));
    }

    [Fact]
    public async Task Renaming_a_shop_changes_what_its_products_say_without_writing_one()
    {
        await RecordShopAsync(_alice, "Alice Cameras");
        var product = await AsSeller(_alice, CreateProductAsync);

        var before = await AsAdmin(() => SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.Equal("Alice Cameras", before!.SellerName);

        // The row's own timestamp, read from the database: the assertion below is that the rename
        // did not touch it (SC-004), and only the row can say that.
        var touchedBefore = await ProductUpdatedAtAsync(product.Id);

        // The rename is a row in the read model and nothing else - the entire reason the shop name
        // is not frozen onto each product.
        await RecordShopAsync(_alice, "Alice Camera Hanoi", DateTime.UtcNow.AddSeconds(1));

        var after = await AsAdmin(() => SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.Equal("Alice Camera Hanoi", after!.SellerName);
        Assert.Equal(touchedBefore, await ProductUpdatedAtAsync(product.Id));
    }

    [Fact]
    public async Task An_overtaken_rename_does_not_win()
    {
        var now = DateTime.UtcNow;
        await RecordShopAsync(_alice, "Newest", now.AddSeconds(10));

        // A message from earlier, arriving late. Comparing names instead of timestamps would let it
        // through and the catalogue would show a name the seller has already changed.
        var recorded = await AsAdmin(() => SendAsync(new RecordSellerCommand(_alice, "Older", now)));

        Assert.False(recorded);
    }

    // ---------------------------------------------------------------- helpers

    private async Task Refused(IRequest request) =>
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(request));

    private async Task Refused<T>(IRequest<T> request) =>
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(request));

    /// <summary>
    /// A shape's photograph is a write like any other: somebody else's is 404 (specs/032).
    /// </summary>
    /// <remarks>
    /// Three ways to be refused here - no such variant, a variant of a different product, and a
    /// variant of somebody else's product - and all three answer with the same sentence. A
    /// difference between them would let a caller map the catalogue by asking.
    /// </remarks>
    [Fact]
    public async Task A_seller_cannot_photograph_another_sellers_variant()
    {
        await RecordShopAsync(_alice, "Alice Cameras");
        var hers = await AsSeller(_alice, CreateProductAsync);

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

        await AsSeller(_bob, async () =>
        {
            var refused = await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(
                new UploadVariantImageCommand(hers.Id, hers.Id, new MemoryStream(png), png.Length)));
            Assert.Contains("was not found", refused.Message);

            var alsoRefused = await Assert.ThrowsAsync<NotFoundException>(
                () => SendAsync(new RemoveVariantImageCommand(hers.Id, hers.Id)));
            Assert.Contains("was not found", alsoRefused.Message);
        });

        // ...and Alice can photograph her own.
        await AsSeller(_alice, () => SendAsync(
            new UploadVariantImageCommand(hers.Id, hers.Id, new MemoryStream(png), png.Length)));
    }

    private Task RecordShopAsync(Guid sellerId, string shopName, DateTime? at = null) =>
        AsAdmin(() => SendAsync(new RecordSellerCommand(sellerId, shopName, at ?? DateTime.UtcNow)));

    private async Task<DateTime> ProductUpdatedAtAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return (await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId)).UpdatedAt;
    }

    private Task<ProductResponse> CreateProductAsync() => CreateProductAsync(null);

    private async Task<ProductResponse> CreateProductAsync(List<VariantOptionInput>? options)
    {
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = $"Own {categoryId:N}"[..20],
                Slug = $"own-{categoryId:N}"[..20],
            });
            await context.SaveChangesAsync();
        }

        var sku = $"OWN{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand(
            $"Camera {sku}", null, 40_000_000m, sku, categoryId, options));
    }

    /// <summary>Runs the body as a seller: that id, and NOT an administrator.</summary>
    private async Task<T> AsSeller<T>(Guid sellerId, Func<Task<T>> body)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = sellerId;
        caller.Roles.Clear();
        caller.Roles.Add("Seller");
        return await body();
    }

    private async Task AsSeller(Guid sellerId, Func<Task> body)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = sellerId;
        caller.Roles.Clear();
        caller.Roles.Add("Seller");
        await body();
    }

    private async Task<T> AsAdmin<T>(Func<Task<T>> body)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
        return await body();
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
}
