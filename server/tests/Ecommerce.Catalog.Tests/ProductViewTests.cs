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
/// What people look at (specs/047): a shopper opening a product page counts; its seller and staff do not,
/// nor a product that is not on the shelf; and simultaneous views are all counted.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ProductViewTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_shopper_opening_a_product_page_counts_and_twenty_at_once_count_twenty()
    {
        var product = await ListedAsync();
        As(Guid.CreateVersion7(), "Customer");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => SendAsync(new RecordProductViewCommand(product.Id))));

        Assert.Equal(20, await ViewsAsync(product.Id));
    }

    [Fact]
    public async Task Staff_and_the_seller_do_not_count_and_neither_does_what_is_not_on_the_shelf()
    {
        var seller = Guid.CreateVersion7();
        var product = await ListedAsync(seller);
        As(seller, "Seller");
        await SendAsync(new RecordProductViewCommand(product.Id));
        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new RecordProductViewCommand(product.Id));

        As(seller, "Seller");
        var pending = await CreateAsync();   // a seller's new product waits for review (specs/045)
        As(Guid.CreateVersion7(), "Customer");
        await SendAsync(new RecordProductViewCommand(pending.Id));
        await SendAsync(new RecordProductViewCommand(Guid.CreateVersion7()));   // no such product: quiet

        Assert.Equal(0, await ViewsAsync(product.Id));
        Assert.Equal(0, await ViewsAsync(pending.Id));
    }

    [Fact]
    public async Task The_most_viewed_come_first()
    {
        var popular = await ListedAsync();
        var quiet = await ListedAsync();
        As(Guid.CreateVersion7(), "Customer");
        for (var i = 0; i < 5; i++) await SendAsync(new RecordProductViewCommand(popular.Id));
        await SendAsync(new RecordProductViewCommand(quiet.Id));

        As(Guid.CreateVersion7(), "Admin");
        var top = await SendAsync(new GetTopViewedQuery(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 50));
        var ours = top.Where(v => v.ProductId == popular.Id || v.ProductId == quiet.Id).ToList();

        Assert.Equal([popular.Id, quiet.Id], ours.Select(v => v.ProductId));
        Assert.Equal(5, ours[0].Views);
    }

    // ------------------------------------------------------------------ helpers

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    /// <summary>A product on the shelf: listed by an administrator, or - with a seller - approved.</summary>
    private async Task<ProductResponse> ListedAsync(Guid? seller = null)
    {
        if (seller is null)
        {
            As(Guid.CreateVersion7(), "Admin");
            return await CreateAsync();
        }

        As(seller.Value, "Seller");
        var product = await CreateAsync();
        As(Guid.CreateVersion7(), "Moderator");
        return await SendAsync(new Ecommerce.Catalog.Application.Products.Review.ApproveProductCommand(product.Id));
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Vw {categoryId:N}"[..20], Slug = $"vw-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"VEW{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Viewed {sku}", null, 1_000_000m, sku, categoryId));
    }

    private async Task<int> ViewsAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().ProductViews
            .Where(v => v.ProductId == productId).SumAsync(v => v.Views);
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
