using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Queries.GetRelatedProducts;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Related products and the listing's <c>ids</c> (specs/163, #375), on a real PostgreSQL: the category first, then the
/// department, most reviewed first, never the product itself and never anything off the shelf.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class RelatedProductsTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    /// <summary>A department with two categories, and another department.</summary>
    private sealed record Shop(Guid Target, Guid MostReviewed, Guid LessReviewed, Guid Sibling, Guid Elsewhere, Guid OffShelf);

    private async Task<Shop> SeedAsync()
    {
        var department = await CategoryAsync(null);
        var cameras = await CategoryAsync(department);
        var lenses = await CategoryAsync(department);
        var books = await CategoryAsync(await CategoryAsync(null));

        var target = await ProductAsync(cameras, reviews: 50);
        var most = await ProductAsync(cameras, reviews: 40);
        var less = await ProductAsync(cameras, reviews: 3);
        var sibling = await ProductAsync(lenses, reviews: 100);
        var elsewhere = await ProductAsync(books, reviews: 500);
        var offShelf = await ProductAsync(cameras, reviews: 90, onShelf: false);

        return new Shop(target, most, less, sibling, elsewhere, offShelf);
    }

    [Fact]
    public async Task The_category_comes_first_most_reviewed_first_then_the_department_and_never_itself()
    {
        var shop = await SeedAsync();

        var related = await RelatedAsync(shop.Target);

        // The lens is the most reviewed of all, but it is in the other category: it comes after the cameras.
        Assert.Equal([shop.MostReviewed, shop.LessReviewed, shop.Sibling], related);
        Assert.DoesNotContain(shop.Target, related);
        Assert.DoesNotContain(shop.OffShelf, related);
        Assert.DoesNotContain(shop.Elsewhere, related);
    }

    [Fact]
    public async Task A_category_with_enough_needs_no_department()
    {
        var shop = await SeedAsync();

        Assert.Equal([shop.MostReviewed, shop.LessReviewed], await RelatedAsync(shop.Target, limit: 2));
    }

    [Fact]
    public async Task An_off_shelf_or_unknown_product_has_none_and_says_nothing_else()
    {
        var shop = await SeedAsync();

        Assert.Empty(await RelatedAsync(shop.OffShelf));
        Assert.Empty(await RelatedAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task A_limit_outside_one_to_twelve_is_refused(int limit)
    {
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetRelatedProductsQuery(Guid.NewGuid(), limit)));
    }

    [Fact]
    public async Task The_listing_by_ids_keeps_exactly_those_on_the_shelf()
    {
        var shop = await SeedAsync();

        var listed = await SendAsync(new GetProductsQuery(PageSize: 50, Ids: [shop.MostReviewed, shop.Sibling, shop.OffShelf, Guid.NewGuid()]));

        Assert.Equal(
            new[] { shop.MostReviewed, shop.Sibling }.Order(),
            listed.Items.Select(p => p.Id).Order());
    }

    [Fact]
    public async Task More_than_24_ids_is_refused()
    {
        var ids = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToList();

        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetProductsQuery(Ids: ids)));
    }

    private async Task<List<Guid>> RelatedAsync(Guid productId, int limit = 8) =>
        (await SendAsync(new GetRelatedProductsQuery(productId, limit))).Select(p => p.Id).ToList();

    private async Task<Guid> CategoryAsync(Guid? parent)
    {
        var id = Guid.CreateVersion7();
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.Categories.Add(new Category { Id = id, Name = $"Rel {id:N}"[..20], Slug = $"rel-{id:N}"[..20], ParentCategoryId = parent });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> ProductAsync(Guid categoryId, int reviews, bool onShelf = true)
    {
        var sku = $"REL{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Rel {sku}", null, 1_000_000m, sku, categoryId));

        // Reviews are counted by the rating recompute (specs/046); a test sets the count it is about.
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.RatingCount, reviews)
            .SetProperty(p => p.RatingAverage, reviews > 0 ? 4.5m : null)
            .SetProperty(p => p.IsActive, onShelf));
        return product.Id;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
