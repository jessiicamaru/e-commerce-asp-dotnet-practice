using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A shop has a page (#197, specs/099): its name and description from Catalog's read model, and its products on the shelf -
/// the public listing, filtered to one seller.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ShopPageTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_shop_lists_only_its_own_products_on_the_shelf()
    {
        var seller = await SellerAsync("Mai Lens");
        var onSale = await ProductAsync(seller, approved: true);
        var waiting = await ProductAsync(seller, approved: false);
        var someoneElses = await ProductAsync(await SellerAsync("Other Shop"), approved: true);

        As(Guid.CreateVersion7(), "Customer");
        var page = await SendAsync(new GetProductsQuery(PageSize: 50, SellerId: seller));

        Assert.Equal([onSale.Id], page.Items.Select(p => p.Id));
        Assert.DoesNotContain(page.Items, p => p.Id == waiting.Id || p.Id == someoneElses.Id);
    }

    [Fact]
    public async Task The_shop_page_has_its_name_description_and_how_much_is_on_sale()
    {
        var seller = await SellerAsync("Mai Lens");
        await ProductAsync(seller, approved: true);
        await ProductAsync(seller, approved: false);
        await SendAsync(new RecordShopDescriptionCommand(seller, "  Used Fujifilm bodies, checked by hand.  ", DateTime.UtcNow));

        var shop = await SendAsync(new GetShopQuery(seller));

        Assert.Equal((seller, "Mai Lens", "Used Fujifilm bodies, checked by hand.", 1), (shop.SellerId, shop.ShopName, shop.Description, shop.ProductCount));
    }

    [Fact]
    public async Task An_unknown_unnamed_or_closed_shop_is_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetShopQuery(Guid.CreateVersion7())));

        var unnamed = Guid.CreateVersion7();   // a description that overtook the registration
        await SendAsync(new RecordShopDescriptionCommand(unnamed, "Soon", DateTime.UtcNow));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetShopQuery(unnamed)));

        var closed = await SellerAsync("Closed Shop");
        await SendAsync(new RecordSellerSuspensionCommand(closed, true, DateTime.UtcNow));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetShopQuery(closed)));
    }

    [Fact]
    public async Task An_older_description_arriving_late_changes_nothing_and_clearing_removes_it()
    {
        var seller = await SellerAsync("Mai Lens");
        var t = DateTime.UtcNow;

        await SendAsync(new RecordShopDescriptionCommand(seller, "Newer", t.AddSeconds(2)));
        Assert.False(await SendAsync(new RecordShopDescriptionCommand(seller, "Older", t.AddSeconds(1))));
        Assert.Equal("Newer", (await SendAsync(new GetShopQuery(seller))).Description);

        await SendAsync(new RecordShopDescriptionCommand(seller, "   ", t.AddSeconds(3)));
        Assert.Null((await SendAsync(new GetShopQuery(seller))).Description);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> SellerAsync(string name)
    {
        var seller = Guid.CreateVersion7();
        await SendAsync(new RecordSellerCommand(seller, name, DateTime.UtcNow.AddMinutes(-1)));
        return seller;
    }

    private async Task<ProductResponse> ProductAsync(Guid seller, bool approved)
    {
        As(seller, "Seller");
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Sh {categoryId:N}"[..20], Slug = $"sh-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"SHP{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Shop {sku}", null, 1_000_000m, sku, categoryId));
        if (approved)
        {
            await using var scope = _fixture.NewScope();
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, ProductReviewStatus.Approved));
        }

        return product;
    }

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
}
