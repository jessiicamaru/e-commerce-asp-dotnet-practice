using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Categories.Translations;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A category describes itself in each language, and falls back to the text it was entered with
/// (specs/026).
/// </summary>
/// <remarks>
/// The same rules as a product's text (specs/021), written a feature later than they should have
/// been: the storefront redesign made an English page full of <c>Máy ảnh không gương lật</c>
/// impossible to keep calling out of scope.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class CategoryTranslationTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task A_translated_category_reads_in_that_language_and_says_which_it_is()
    {
        var category = await CreateCategoryAsync("Máy ảnh không gương lật", "Ống kính rời");

        await SendAsync(new SetCategoryTranslationCommand(
            category.Id, "en", "Mirrorless cameras", "Interchangeable lenses"));

        var english = await ReadAsync(category.Id, "en");
        Assert.Equal("Mirrorless cameras", english.Name);
        Assert.Equal("Interchangeable lenses", english.Description);
        Assert.Equal("en", english.Language);

        var vietnamese = await ReadAsync(category.Id, "vi");
        // The name as CREATED - the helper makes it unique so two tests cannot clash on the slug.
        Assert.Equal(category.Name, vietnamese.Name);
        Assert.Equal("vi", vietnamese.Language);
    }

    [Fact]
    public async Task An_untranslated_category_shows_its_own_text_and_says_it_is_the_default()
    {
        var category = await CreateCategoryAsync("Máy ảnh compact", null);

        var english = await ReadAsync(category.Id, "en");

        // The text as entered, and `language` says "vi" - which is how a client tells "this is
        // English" from "nobody has written the English yet".
        Assert.Equal(category.Name, english.Name);
        Assert.StartsWith("Máy ảnh compact", english.Name);
        Assert.Equal("vi", english.Language);
    }

    [Fact]
    public async Task The_fallback_is_per_field_like_a_products()
    {
        var category = await CreateCategoryAsync("Máy ảnh compact", "Ống kính liền");

        // A name translated and a description not: the half somebody got to still shows.
        await SendAsync(new SetCategoryTranslationCommand(category.Id, "en", "Compact cameras", null));

        var english = await ReadAsync(category.Id, "en");
        Assert.Equal("Compact cameras", english.Name);
        Assert.Equal("Ống kính liền", english.Description);
    }

    [Fact]
    public async Task Writing_a_translation_twice_replaces_it_rather_than_conflicting()
    {
        var category = await CreateCategoryAsync("Máy ảnh compact", null);

        await SendAsync(new SetCategoryTranslationCommand(category.Id, "en", "Compacts", null));
        await SendAsync(new SetCategoryTranslationCommand(category.Id, "en", "Compact cameras", null));

        Assert.Equal("Compact cameras", (await ReadAsync(category.Id, "en")).Name);
    }

    [Fact]
    public async Task Removing_a_translation_puts_the_category_back_to_its_own_text()
    {
        var category = await CreateCategoryAsync("Máy ảnh compact", null);
        await SendAsync(new SetCategoryTranslationCommand(category.Id, "en", "Compact cameras", null));

        await SendAsync(new RemoveCategoryTranslationCommand(category.Id, "en"));

        Assert.Equal(category.Name, (await ReadAsync(category.Id, "en")).Name);
    }

    [Fact]
    public async Task A_language_the_shop_does_not_speak_is_refused_rather_than_stored()
    {
        var category = await CreateCategoryAsync("Máy ảnh compact", null);

        // A row nobody would ever read is a lie about what the shop offers.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new SetCategoryTranslationCommand(category.Id, "fr", "Appareils compacts", null)));
    }

    [Fact]
    public async Task Translating_a_category_that_is_not_there_is_a_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new SetCategoryTranslationCommand(Guid.CreateVersion7(), "en", "Nothing", null)));
    }

    private async Task<CategoryResponse> ReadAsync(Guid categoryId, string language)
    {
        var listed = await SendAsync(new GetCategoriesQuery(), language);
        return listed.Single(c => c.Id == categoryId);
    }

    private async Task<CategoryResponse> CreateCategoryAsync(string name, string? description)
    {
        var id = Guid.CreateVersion7();
        return await SendAsync(new CreateCategoryCommand(
            $"{name} {id:N}"[..Math.Min(100, name.Length + 9)], description, $"cat-{id:N}"[..20], null));
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
