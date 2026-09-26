using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A product describes itself in each language, and falls back to the text it was entered with
/// (specs/021).
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class TranslationTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task A_translated_product_reads_in_that_language_and_says_which_it_is()
    {
        var product = await CreateProductAsync("Sony A7 IV", "Full-frame body");

        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh Sony A7 IV", "Thân máy full-frame"));

        var vietnamese = await ReadAsync(product.Id, "vi");
        Assert.Equal("Máy ảnh Sony A7 IV", vietnamese.Name);
        Assert.Equal("Thân máy full-frame", vietnamese.Description);
        Assert.Equal("vi", vietnamese.Language);

        // English has no translation, so it shows the text the product was entered with - and says so.
        var english = await ReadAsync(product.Id, "en");
        Assert.Equal("Sony A7 IV", english.Name);
        Assert.Equal("vi", english.Language);   // the default language, meaning "not translated"
    }

    [Fact]
    public async Task The_fallback_is_per_field_not_per_product()
    {
        // Half-translated is the normal state of a shop being translated: the customer should see every
        // word somebody has got to, not lose the name because the description is missing.
        var product = await CreateProductAsync("Sony A7 IV", "Full-frame body");

        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh Sony A7 IV", Description: null));

        var vietnamese = await ReadAsync(product.Id, "vi");
        Assert.Equal("Máy ảnh Sony A7 IV", vietnamese.Name);
        Assert.Equal("Full-frame body", vietnamese.Description);
    }

    [Fact]
    public async Task Writing_a_translation_twice_replaces_it()
    {
        var product = await CreateProductAsync("Sony A7 IV", null);

        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh", null));
        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh Sony", null));

        Assert.Equal("Máy ảnh Sony", (await ReadAsync(product.Id, "vi")).Name);
    }

    [Fact]
    public async Task Removing_a_translation_returns_the_product_to_its_own_text()
    {
        var product = await CreateProductAsync("Sony A7 IV", null);
        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh Sony", null));

        await SendAsync(new RemoveProductTranslationCommand(product.Id, "vi"));
        await SendAsync(new RemoveProductTranslationCommand(product.Id, "vi"));   // twice is quiet

        Assert.Equal("Sony A7 IV", (await ReadAsync(product.Id, "vi")).Name);
    }

    [Fact]
    public async Task A_language_the_shop_does_not_speak_cannot_be_written()
    {
        var product = await CreateProductAsync("Sony A7 IV", null);

        // Nothing would ever read it, and a row nobody reads is a lie about what the shop offers.
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new SetProductTranslationCommand(product.Id, "fr", "Appareil photo", null)));
    }

    [Fact]
    public async Task What_a_customer_chooses_between_is_translated_too()
    {
        var product = await CreateProductAsync("Sony A7 IV", null);
        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 1400m, [new VariantOptionInput("Kit", "Body only")]));

        var optionId = await OptionIdAsync(kit.Id);
        await SendAsync(new SetOptionTranslationCommand(product.Id, optionId, "vi", "Bộ", "Chỉ thân máy"));

        var vietnamese = await ReadAsync(product.Id, "vi");
        var variant = vietnamese.Variants!.Single(v => v.Id == kit.Id);
        Assert.Equal("Bộ: Chỉ thân máy", variant.OptionSummary);
        Assert.Equal("Bộ", variant.Options[0].Name);

        // English still reads what was entered - a shop with Vietnamese names and English colours is
        // not translated, which is why the option carries its own translation.
        var english = await ReadAsync(product.Id, "en");
        Assert.Equal("Kit: Body only", english.Variants!.Single(v => v.Id == kit.Id).OptionSummary);
    }

    [Fact]
    public async Task An_option_belonging_to_another_product_is_not_found()
    {
        var mine = await CreateProductAsync("Mine", null);
        var theirs = await CreateProductAsync("Theirs", null);
        var theirVariant = await SendAsync(new AddProductVariantCommand(
            theirs.Id, $"{theirs.Sku}-K", 5m, [new VariantOptionInput("Colour", "Black")]));

        var optionId = await OptionIdAsync(theirVariant.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new SetOptionTranslationCommand(mine.Id, optionId, "vi", "Màu", "Đen")));
    }

    [Fact]
    public async Task Searching_ignores_diacritics_and_looks_at_both_the_translation_and_the_original()
    {
        var product = await CreateProductAsync($"Camera {Guid.NewGuid():N}"[..20], null);
        var marker = product.Name[^8..];
        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", $"Máy ảnh {marker}", null));

        // Typed without the accents, which is how people search.
        var withoutAccents = await SearchAsync($"may anh {marker}", "vi");
        Assert.Contains(withoutAccents, p => p.Id == product.Id);

        // And with them.
        Assert.Contains(await SearchAsync($"Máy ảnh {marker}", "vi"), p => p.Id == product.Id);

        // The original text is searched as well, so a shop half-translated still finds everything -
        // this product is found by the name it was entered with, while asking in Vietnamese.
        Assert.Contains(await SearchAsync(marker, "vi"), p => p.Id == product.Id);
    }

    /// <summary>
    /// The search is a LIKE since specs/074 (#113) - so a term's own % and _ are escaped and mean themselves:
    /// "50%" is not "50 followed by anything", and "a_b" is not "a, any one character, b".
    /// </summary>
    [Fact]
    public async Task A_term_with_percent_or_underscore_is_matched_literally()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var percent = await CreateProductAsync($"Sale 50% {marker}", null);
        var fifty = await CreateProductAsync($"Sale 5000 {marker}", null);
        var underscore = await CreateProductAsync($"Lens a_b {marker}", null);
        var axb = await CreateProductAsync($"Lens axb {marker}", null);

        var byPercent = await SearchAsync($"50% {marker}", "en");
        var byUnderscore = await SearchAsync($"a_b {marker}", "en");

        Assert.Equal([percent.Id], byPercent.Select(p => p.Id));
        Assert.DoesNotContain(byPercent, p => p.Id == fifty.Id);
        Assert.Equal([underscore.Id], byUnderscore.Select(p => p.Id));
        Assert.DoesNotContain(byUnderscore, p => p.Id == axb.Id);
    }

    [Fact]
    public async Task Deleting_a_product_takes_its_translations_with_it()
    {
        var product = await CreateProductAsync("Sony A7 IV", null);
        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh Sony", null));

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var variants = await context.ProductVariants.Where(v => v.ProductId == product.Id).ToListAsync();
        context.ProductVariants.RemoveRange(variants);
        context.Products.Remove(await context.Products.SingleAsync(p => p.Id == product.Id));
        await context.SaveChangesAsync();

        Assert.Empty(await context.ProductTranslations.Where(t => t.ProductId == product.Id).ToListAsync());
    }

    private async Task<Guid> OptionIdAsync(Guid variantId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.VariantOptions.Where(o => o.VariantId == variantId).Select(o => o.Id).SingleAsync();
    }

    private async Task<List<ProductResponse>> SearchAsync(string term, string language)
    {
        var page = await SendAsync(new GetProductsQuery { SearchTerm = term, PageSize = 50 }, language);
        return page.Items;
    }

    private async Task<ProductResponse> ReadAsync(Guid productId, string language) =>
        (await SendAsync(new GetProductByIdQuery(productId), language))!;

    private async Task<ProductResponse> CreateProductAsync(string name, string? description)
    {
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = $"Loc {categoryId:N}"[..20],
                Slug = $"loc-{categoryId:N}"[..20],
            });
            await context.SaveChangesAsync();
        }

        var sku = $"LOC{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand(name, description, 100m, sku, categoryId));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request, string? language = null)
    {
        await using var scope = _fixture.NewScope(language);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request, string? language = null)
    {
        await using var scope = _fixture.NewScope(language);
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
