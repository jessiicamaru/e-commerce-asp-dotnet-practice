using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.UpdateCategory;
using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Categories.Translations;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Administrators manage categories (#195, specs/097): a category's own name can finally be changed, its slug never is,
/// and a slug already taken is a 409 rather than the 500 a bare exception made it.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class CategoryAdminTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task A_rename_changes_the_default_text_keeps_the_slug_and_is_on_the_record()
    {
        var category = await CreateAsync("Ong kinh");

        var renamed = await SendAsync(new UpdateCategoryCommand(category.Id, "Ống kính rời", "Cho Sony E"));

        Assert.Equal(("Ống kính rời", "Cho Sony E", category.Slug), (renamed.Name, renamed.Description, renamed.Slug));
        var listed = (await SendAsync(new GetCategoriesQuery(), "vi")).Single(c => c.Id == category.Id);
        Assert.Equal("Ống kính rời", listed.Name);
        var entry = Assert.Single(Audited(category.Id, "CategoryUpdated"));
        Assert.Contains("Ong kinh", entry.Before);
        Assert.Contains("Sony E", entry.After);   // the snapshot escapes what is not ASCII
    }

    [Fact]
    public async Task A_rename_leaves_the_english_translation_alone()
    {
        var category = await CreateAsync("May anh");
        await SendAsync(new SetCategoryTranslationCommand(category.Id, "en", "Cameras", null));

        await SendAsync(new UpdateCategoryCommand(category.Id, "Máy ảnh", null));

        Assert.Equal("Cameras", (await SendAsync(new GetCategoriesQuery(), "en")).Single(c => c.Id == category.Id).Name);
    }

    [Fact]
    public async Task An_unknown_category_is_404_and_an_empty_name_is_400()
    {
        var category = await CreateAsync("Den flash");

        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new UpdateCategoryCommand(Guid.CreateVersion7(), "X", null)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new UpdateCategoryCommand(category.Id, " ", null)));
    }

    [Fact]
    public async Task A_slug_already_taken_is_a_conflict_not_a_server_error()
    {
        var category = await CreateAsync("Chan may");

        var refused = await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new CreateCategoryCommand("Chân máy khác", null, category.Slug, null)));
        Assert.Contains(category.Slug, refused.Message);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<CategoryResponse> CreateAsync(string name)
    {
        var id = Guid.CreateVersion7();
        return await SendAsync(new CreateCategoryCommand($"{name} {id:N}"[..(name.Length + 9)], null, $"adm-{id:N}"[..20], null));
    }

    private List<AuditEntryRecorded> Audited(Guid categoryId, string action) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == categoryId.ToString() && e.Action == action)
            .ToList();

    private async Task<T> SendAsync<T>(IRequest<T> request, string? language = null)
    {
        await using var scope = _fixture.NewScope(language);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
