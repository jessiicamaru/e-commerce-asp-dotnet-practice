using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Products.Views;
using Ecommerce.Catalog.Application.Reviews;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A shop's rating (#379, specs/165): every visible review of every product the seller has, weighted by each product's
/// count, on the shop's page - the same number the seller's own insights show (specs/068).
/// </summary>
/// <remarks>
/// The reviews are real ones, written through the commands a customer and a moderator send, so the per-product average
/// the shop's is built from is the one <see cref="ReviewTests"/> proves.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class ShopRatingTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    /// <summary>4 once and 2 three times is 2.5 over 4 - not 3, the average of the two averages.</summary>
    [Fact]
    public async Task The_shop_page_weights_each_product_by_its_reviews_and_agrees_with_the_insights()
    {
        var seller = await SellerAsync("Mai Lens");
        var loved = await ProductAsync(seller);
        var disliked = await ProductAsync(seller);
        await ProductAsync(seller);   // no review: not a zero in the average
        await ReviewAsync(loved, 4);
        await ReviewAsync(disliked, 2);
        await ReviewAsync(disliked, 2);
        await ReviewAsync(disliked, 2);

        var shop = await SendAsync(new GetShopQuery(seller));
        As(seller, "Seller");
        var insights = await SendAsync(new GetMyProductInsightsQuery());

        Assert.Equal((2.50m, 4), (shop.RatingAverage, shop.RatingCount));
        Assert.Equal((shop.RatingAverage, shop.RatingCount), (insights.RatingAverage, insights.RatingCount));
    }

    [Fact]
    public async Task A_shop_with_no_review_has_no_rating_and_another_shops_reviews_are_not_its_own()
    {
        var quiet = await SellerAsync("Quiet Shop");
        await ProductAsync(quiet);
        var busy = await SellerAsync("Busy Shop");
        await ReviewAsync(await ProductAsync(busy), 5);

        var shop = await SendAsync(new GetShopQuery(quiet));

        Assert.Equal(((decimal?)null, 0), (shop.RatingAverage, shop.RatingCount));
    }

    [Fact]
    public async Task A_hidden_review_leaves_the_shops_rating_and_a_restored_one_comes_back()
    {
        var seller = await SellerAsync("Mai Lens");
        var product = await ProductAsync(seller);
        await ReviewAsync(product, 5);
        var spam = await ReviewAsync(product, 1);

        AsStaff();
        await SendAsync(new HideReviewCommand(spam.Id, "Advertising"));
        Assert.Equal((5.00m, 1), await RatingAsync(seller));

        await SendAsync(new RestoreReviewCommand(spam.Id));
        Assert.Equal((3.00m, 2), await RatingAsync(seller));
    }

    /// <summary>Research D2: withdrawing a badly reviewed product must not lift the shop's rating.</summary>
    [Fact]
    public async Task A_product_off_the_shelf_still_counts()
    {
        var seller = await SellerAsync("Mai Lens");
        var good = await ProductAsync(seller);
        var bad = await ProductAsync(seller);
        await ReviewAsync(good, 5);
        await ReviewAsync(bad, 1);

        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == bad.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsActive, false));
        }

        var shop = await SendAsync(new GetShopQuery(seller));

        Assert.Equal((3.00m, 2, 1), (shop.RatingAverage, shop.RatingCount, shop.ProductCount));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(decimal?, int)> RatingAsync(Guid seller)
    {
        var shop = await SendAsync(new GetShopQuery(seller));
        return (shop.RatingAverage, shop.RatingCount);
    }

    private async Task<Guid> SellerAsync(string name)
    {
        var seller = Guid.CreateVersion7();
        await SendAsync(new RecordSellerCommand(seller, name, DateTime.UtcNow.AddMinutes(-1)));
        return seller;
    }

    /// <summary>A seller's product, approved - what a customer can buy and so receive.</summary>
    private async Task<ProductResponse> ProductAsync(Guid seller)
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Sr {categoryId:N}"[..20], Slug = $"sr-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        As(seller, "Seller");
        var sku = $"SRT{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Rated {sku}", null, 1_000_000m, sku, categoryId));
        AsStaff();
        return await SendAsync(new ApproveProductCommand(product.Id));
    }

    /// <summary>A new customer receives the product (what Order's ParcelDeliveredEvent does) and reviews it.</summary>
    private async Task<ReviewResponse> ReviewAsync(ProductResponse product, int rating)
    {
        var customer = Guid.CreateVersion7();
        await SendAsync(new RecordReviewEligibilityCommand(customer, [product.Id], DateTime.UtcNow));
        As(customer, "Customer");
        _fixture.Services.GetRequiredService<TestCaller>().GivenName = "Lan";
        return await SendAsync(new WriteReviewCommand(product.Id, rating, null));
    }

    private void AsStaff() => As(Guid.CreateVersion7(), "Moderator");

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
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
