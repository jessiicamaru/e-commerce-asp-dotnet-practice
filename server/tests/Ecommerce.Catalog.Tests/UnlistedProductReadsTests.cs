using System.Text.RegularExpressions;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using Ecommerce.Catalog.Application.Products.Images.UploadProductImage;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Questions;
using Ecommerce.Catalog.Application.Reviews;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A product off the shelf is the public lookup's 404 (specs/045) - and since specs/081 (#166) so is what hangs on
/// it: its photograph, its reviews and its questions. Its seller and staff still see all three; the photograph they
/// reach through an address carrying the image's own key, because a browser's image request carries no token.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class UnlistedProductReadsTests(CatalogTestFixture fixture) : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 8, 1];
    private static readonly byte[] Other = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 8, 2];

    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task On_sale_the_photograph_is_everybodys_with_or_without_the_key()
    {
        var product = await ListedWithImageAsync();
        As(Guid.CreateVersion7(), "Customer");

        var plain = await SendAsync(new GetProductImageQuery(product.Id));
        var keyed = await SendAsync(new GetProductImageQuery(product.Id, KeyIn(product.ImageUrl)));

        Assert.True(plain is { Public: true });
        Assert.True(keyed is { Public: true });
        Assert.Matches("&k=[0-9a-f]{32}$", product.ImageUrl);
    }

    /// <summary>The issue's probe: taken down, the id alone opens nothing - not even the address it had while on sale.</summary>
    [Fact]
    public async Task Off_the_shelf_the_photograph_needs_its_key_and_the_key_alone_opens_it()
    {
        var product = await ListedWithImageAsync();
        var key = KeyIn(product.ImageUrl);
        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new TakeDownProductCommand(product.Id, "Counterfeit"));
        As(Guid.CreateVersion7(), "Customer");

        Assert.Null(await SendAsync(new GetProductImageQuery(product.Id)));
        Assert.Null(await SendAsync(new GetProductImageQuery(product.Id, Guid.NewGuid())));
        var served = await SendAsync(new GetProductImageQuery(product.Id, key));

        Assert.True(served is { Public: false });   // never for a shared cache
    }

    /// <summary>What the seller and staff are given is what opens it: the address in the product they may read.</summary>
    [Fact]
    public async Task A_sellers_waiting_product_shows_its_photograph_to_its_seller_and_staff_only()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var pending = await CreateAsync();
        await SendAsync(new UploadProductImageCommand(pending.Id, new MemoryStream(Png), Png.Length));

        var sellers = await SendAsync(new GetProductByIdQuery(pending.Id));
        As(Guid.CreateVersion7(), "Moderator");
        var staffs = await SendAsync(new GetProductByIdQuery(pending.Id));
        As(Guid.CreateVersion7(), "Customer");
        var shoppers = await SendAsync(new GetProductByIdQuery(pending.Id));

        Assert.Null(shoppers);
        Assert.Equal(sellers!.ImageUrl, staffs!.ImageUrl);
        Assert.NotNull(await SendAsync(new GetProductImageQuery(pending.Id, KeyIn(sellers.ImageUrl))));
        Assert.Null(await SendAsync(new GetProductImageQuery(pending.Id)));
    }

    /// <summary>A new photograph, a new key: an address handed out for the old one opens nothing while off the shelf.</summary>
    [Fact]
    public async Task Replacing_the_photograph_retires_the_old_key()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var pending = await CreateAsync();
        var first = await SendAsync(new UploadProductImageCommand(pending.Id, new MemoryStream(Png), Png.Length));
        await Task.Delay(5);
        var second = await SendAsync(new UploadProductImageCommand(pending.Id, new MemoryStream(Other), Other.Length));

        Assert.NotEqual(KeyIn(first.ImageUrl), KeyIn(second.ImageUrl));
        Assert.Null(await SendAsync(new GetProductImageQuery(pending.Id, KeyIn(first.ImageUrl))));
        Assert.NotNull(await SendAsync(new GetProductImageQuery(pending.Id, KeyIn(second.ImageUrl))));
    }

    [Fact]
    public async Task A_variants_own_photograph_follows_the_same_rule()
    {
        var product = await ListedWithImageAsync();
        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new UploadVariantImageCommand(product.Id, product.Id, new MemoryStream(Other), Other.Length));
        var withVariant = await SendAsync(new GetProductByIdQuery(product.Id));
        var variantUrl = withVariant!.Variants!.Single().ImageUrl;
        await SendAsync(new TakeDownProductCommand(product.Id, "Counterfeit"));
        As(Guid.CreateVersion7(), "Customer");

        Assert.Contains("/variants/", variantUrl);
        Assert.Null(await SendAsync(new GetVariantImageQuery(product.Id, product.Id)));
        Assert.True(await SendAsync(new GetVariantImageQuery(product.Id, product.Id, KeyIn(variantUrl))) is { Public: false });
    }

    /// <summary>What is said about a product off the shelf is the product's page's to show - its seller's and staff's.</summary>
    [Fact]
    public async Task Off_the_shelf_its_reviews_and_questions_are_a_404_but_to_its_seller_and_staff()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var pending = await CreateAsync();

        As(Guid.CreateVersion7(), "Customer");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetProductReviewsQuery(pending.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetProductQuestionsQuery(pending.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetProductReviewsQuery(Guid.CreateVersion7())));
        As(Guid.CreateVersion7(), "Seller");   // another seller
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetProductQuestionsQuery(pending.Id)));

        As(seller, "Seller");
        Assert.Equal(0, (await SendAsync(new GetProductReviewsQuery(pending.Id))).TotalCount);
        As(Guid.CreateVersion7(), "Moderator");
        Assert.Equal(0, (await SendAsync(new GetProductQuestionsQuery(pending.Id))).TotalCount);
    }

    /// <summary>
    /// #174 (specs/085): reading was closed in specs/081, writing was not - a customer who once received a product
    /// could still write or change a review of it off the shelf, and move a rating nobody could see. Now writing is
    /// the same 404 as asking a question there, the page says "not eligible", and back on sale it works again.
    /// </summary>
    [Fact]
    public async Task Off_the_shelf_nobody_writes_a_review_and_its_rating_does_not_move()
    {
        As(Guid.CreateVersion7(), "Admin");
        var product = await CreateAsync();
        var buyer = Guid.CreateVersion7();
        await SendAsync(new RecordReviewEligibilityCommand(buyer, [product.Id], DateTime.UtcNow));
        As(buyer, "Customer");
        await SendAsync(new WriteReviewCommand(product.Id, 5, "Sharp and light."));

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new TakeDownProductCommand(product.Id, "Counterfeit"));

        As(buyer, "Customer");
        var refused = await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new WriteReviewCommand(product.Id, 1, "Changed my mind.")));
        Assert.Equal("Product not found.", refused.Message);   // the same words as a product that does not exist
        Assert.False((await SendAsync(new GetMyReviewQuery(product.Id))).Eligible);
        Assert.Equal((5m, 1), await RatingAsync(product.Id));

        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, ProductReviewStatus.Approved));
        }

        Assert.Equal(4, (await SendAsync(new WriteReviewCommand(product.Id, 4, "Fine again."))).Rating);
        Assert.Equal((4m, 1), await RatingAsync(product.Id));
    }

    [Fact]
    public async Task On_sale_its_reviews_and_questions_are_everybodys()
    {
        var product = await ListedWithImageAsync();
        As(Guid.CreateVersion7(), "Customer");

        Assert.Equal(0, (await SendAsync(new GetProductReviewsQuery(product.Id))).TotalCount);
        Assert.Equal(0, (await SendAsync(new GetProductQuestionsQuery(product.Id))).TotalCount);
    }

    // ------------------------------------------------------------------ helpers

    private static Guid? KeyIn(string? url) =>
        url is not null && Regex.Match(url, "[?&]k=([0-9a-f]{32})") is { Success: true } m ? Guid.Parse(m.Groups[1].Value) : null;

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task<ProductResponse> ListedWithImageAsync()
    {
        As(Guid.CreateVersion7(), "Admin");
        var product = await CreateAsync();
        return await SendAsync(new UploadProductImageCommand(product.Id, new MemoryStream(Png), Png.Length));
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Ul {categoryId:N}"[..20], Slug = $"ul-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"UNL{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Unlisted {sku}", null, 1_000_000m, sku, categoryId));
    }

    private async Task<(decimal?, int)> RatingAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        return (product.RatingAverage, product.RatingCount);
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
