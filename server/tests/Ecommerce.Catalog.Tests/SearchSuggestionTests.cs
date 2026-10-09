using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Queries.SuggestProducts;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Search suggestions (specs/164, #376), on a real PostgreSQL: products through the catalogue's own search - diacritics
/// ignored, on the shelf only - and categories matched alike; a handful of each.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class SearchSuggestionTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    // Each test's own word, so other tests' rows in the shared database never match.
    private static string Token() => $"zq{Guid.NewGuid():N}"[..10];

    [Fact]
    public async Task A_term_without_diacritics_finds_products_and_categories_with_them()
    {
        var token = Token();
        var category = await CategoryAsync($"Máy ảnh {token}");
        var camera = await ProductAsync($"Máy ảnh {token} Alpha", category);

        var found = await SuggestAsync($"may anh {token}");

        Assert.Equal([camera], found.Products.Select(p => p.Id));
        Assert.Equal([category], found.Categories.Select(c => c.Id));
        Assert.Equal($"Máy ảnh {token} Alpha", found.Products[0].Name);
        Assert.Equal(1_000_000m, found.Products[0].Price);
    }

    [Fact]
    public async Task Nothing_off_the_shelf_and_every_category_the_list_shows()
    {
        var token = Token();
        // As every seeded category is: `IsActive` is never set, and the categories list shows them all (research D6).
        var shown = await CategoryAsync($"Shown {token}", active: false);
        await ProductAsync($"Off {token}", shown, onShelf: false);

        var found = await SuggestAsync(token);

        Assert.Empty(found.Products);
        Assert.Equal([shown], found.Categories.Select(c => c.Id));
    }

    [Fact]
    public async Task At_most_six_products_and_four_categories()
    {
        var token = Token();
        var category = await CategoryAsync($"Cat0 {token}");
        for (var i = 1; i < 6; i++)
        {
            await CategoryAsync($"Cat{i} {token}");
        }

        for (var i = 0; i < 8; i++)
        {
            await ProductAsync($"Item{i} {token}", category);
        }

        var found = await SuggestAsync(token);

        Assert.Equal(SuggestProductsQueryHandler.MaxProducts, found.Products.Count);
        Assert.Equal(SuggestProductsQueryHandler.MaxCategories, found.Categories.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" a ")]
    public async Task Fewer_than_two_characters_is_refused(string q)
    {
        await Assert.ThrowsAsync<ValidationException>(() => SuggestAsync(q));
    }

    [Theory]
    [InlineData("Máy ảnh", "may anh")]
    [InlineData("Đồng hồ", "dong ho")]
    [InlineData("SÁCH", "sach")]
    public void Folding_matches_the_search(string text, string folded)
    {
        Assert.Equal(folded, SuggestProductsQueryHandler.Fold(text));
    }

    private async Task<SearchSuggestions> SuggestAsync(string q)
    {
        // Asked in Vietnamese and dong, said rather than inherited: the fixture's language and currency are shared, and
        // another test may have left them on dollars.
        await using var scope = _fixture.NewScope("vi", new Currency("VND", 0));
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SuggestProductsQuery(q));
    }

    private async Task<Guid> CategoryAsync(string name, bool active = true)
    {
        var id = Guid.CreateVersion7();
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.Categories.Add(new Category { Id = id, Name = name, Slug = $"sg-{id:N}"[..20], IsActive = active });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> ProductAsync(string name, Guid categoryId, bool onShelf = true)
    {
        var sku = $"SG{Guid.NewGuid():N}"[..20];
        await using var scope = _fixture.NewScope();
        var product = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateProductCommand(name, null, 1_000_000m, sku, categoryId));
        if (!onShelf)
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await context.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.IsActive, false));
        }

        return product.Id;
    }
}
