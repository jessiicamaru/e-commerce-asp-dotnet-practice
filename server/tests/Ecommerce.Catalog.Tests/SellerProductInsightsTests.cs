using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Views;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A seller's own views and ratings (specs/068, #111): only their products, views inside the period, and a
/// rating over every visible review - weighted by each product's count, not the average of averages.
/// </summary>
/// <remarks>
/// Views and ratings are written straight into their tables: how they get there - the view upsert, the
/// recomputed average - is what <see cref="ProductViewTests"/> and <see cref="ReviewTests"/> prove.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class SellerProductInsightsTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Counts_the_sellers_own_products_views_inside_the_period_most_viewed_first()
    {
        var mai = Guid.CreateVersion7();
        var popular = await OwnedAsync(mai);
        var quiet = await OwnedAsync(mai);
        var someoneElses = await OwnedAsync(Guid.CreateVersion7());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await ViewsAsync(popular.Id, today, 7);
        await ViewsAsync(popular.Id, today.AddDays(-1), 3);
        await ViewsAsync(popular.Id, today.AddDays(-40), 500);   // outside the period
        await ViewsAsync(quiet.Id, today, 2);
        await ViewsAsync(someoneElses.Id, today, 900);           // not hers

        var mine = await AsSellerAsync(mai, new GetMyProductInsightsQuery(DateTime.UtcNow.AddDays(-6), DateTime.UtcNow));

        Assert.Equal(12, mine.Views);
        Assert.Equal([(popular.Id, 10), (quiet.Id, 2)], mine.Products.Select(p => (p.ProductId, p.Views)));
    }

    /// <summary>4.0 over 1 review and 2.0 over 3 is 2.5 overall - not 3.0, the average of the two averages.</summary>
    [Fact]
    public async Task The_shops_rating_is_weighted_by_each_products_review_count()
    {
        var mai = Guid.CreateVersion7();
        var loved = await OwnedAsync(mai);
        var disliked = await OwnedAsync(mai);
        await OwnedAsync(mai);   // no reviews: not a zero in the average
        await RatedAsync(loved.Id, 4.0m, 1);
        await RatedAsync(disliked.Id, 2.0m, 3);

        var mine = await AsSellerAsync(mai, new GetMyProductInsightsQuery());

        Assert.Equal((2.50m, 4), (mine.RatingAverage, mine.RatingCount));
        Assert.Equal((4.0m, 1), mine.Products.Where(p => p.ProductId == loved.Id).Select(p => (p.RatingAverage, p.RatingCount)).Single());
    }

    [Fact]
    public async Task A_seller_with_nothing_yet_has_no_rating_rather_than_a_zero()
    {
        var mai = Guid.CreateVersion7();
        var product = await OwnedAsync(mai);

        var mine = await AsSellerAsync(mai, new GetMyProductInsightsQuery());

        Assert.Equal((0, (decimal?)null, 0), (mine.Views, mine.RatingAverage, mine.RatingCount));
        Assert.Equal(product.Id, Assert.Single(mine.Products).ProductId);   // listed anyway, with no views
    }

    [Fact]
    public async Task The_list_is_limited_and_the_totals_are_not()
    {
        var mai = Guid.CreateVersion7();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 1; i <= 3; i++)
            await ViewsAsync((await OwnedAsync(mai)).Id, today, i);

        var mine = await AsSellerAsync(mai, new GetMyProductInsightsQuery(Limit: 2));

        Assert.Equal(2, mine.Products.Count);
        Assert.Equal([3, 2], mine.Products.Select(p => p.Views));
        Assert.Equal(6, mine.Views);
    }

    [Fact]
    public async Task The_period_rule_is_every_insights_rule()
    {
        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            AsSellerAsync(Guid.CreateVersion7(), new GetMyProductInsightsQuery(DateTime.UtcNow.AddDays(-400), DateTime.UtcNow)));
        Assert.Contains("at most 366 days", refused.Message);
    }

    [Fact]
    public async Task A_caller_without_an_id_is_refused()
    {
        As(Guid.Empty, "Seller");
        _fixture.Services.GetRequiredService<TestCaller>().Id = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new GetMyProductInsightsQuery()));
    }

    // ------------------------------------------------------------------ helpers

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task<T> AsSellerAsync<T>(Guid seller, IRequest<T> request)
    {
        As(seller, "Seller");
        return await SendAsync(request);
    }

    /// <summary>A product of this seller's; whether it is on the shelf does not matter to their own insights.</summary>
    private async Task<ProductResponse> OwnedAsync(Guid seller)
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Si {categoryId:N}"[..20], Slug = $"si-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        As(seller, "Seller");
        var sku = $"SIN{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Insight {sku}", null, 1_000_000m, sku, categoryId));
    }

    private async Task ViewsAsync(Guid productId, DateOnly day, int views)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.ProductViews.Add(new ProductView { ProductId = productId, Day = day, Views = views });
        await context.SaveChangesAsync();
    }

    private async Task RatedAsync(Guid productId, decimal average, int count)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.RatingAverage, average).SetProperty(p => p.RatingCount, count));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
