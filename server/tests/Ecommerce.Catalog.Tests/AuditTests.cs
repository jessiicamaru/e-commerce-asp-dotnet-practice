using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Translations;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Contracts.Activity;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// What Catalog tells the audit log (specs/041): listing, pricing and withdrawing a product - one entry
/// each, with the diff a person can read.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class AuditTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task Listing_pricing_and_withdrawing_are_each_recorded_once()
    {
        var id = Guid.CreateVersion7();
        var category = await SendAsync(new CreateCategoryCommand($"Aud {id:N}"[..20], null, $"aud-{id:N}"[..20], null));
        var sku = $"AUD{Guid.NewGuid():N}"[..20];

        var product = await SendAsync(new CreateProductCommand($"Camera {sku}", null, 1_000_000m, sku, category.Id));
        var variant = product.Variants.Single();
        await SendAsync(new SetVariantPriceCommand(product.Id, variant.Id, "VND", 1_200_000m));
        await SendAsync(new DeleteProductCommand(product.Id));

        var entries = Entries(product.Id.ToString(), variant.Id.ToString());

        Assert.Equal(["ProductCreated", "PriceSet", "ProductDeleted"], entries.Select(e => e.Action));
        Assert.All(entries, e => Assert.Equal("Catalog", e.Category));
        Assert.All(entries, e => Assert.Equal("Admin", e.ActorRole));

        var priced = entries.Single(e => e.Action == "PriceSet");
        Assert.Contains("1000000", priced.Before);
        Assert.Contains("1200000", priced.After);

        Assert.Null(entries.Single(e => e.Action == "ProductCreated").Before);
        Assert.Null(entries.Single(e => e.Action == "ProductDeleted").After);
    }

    /// <summary>A refused change leaves no entry: nothing was saved, so nothing is claimed to have happened.</summary>
    [Fact]
    public async Task A_refused_change_is_not_recorded()
    {
        var missing = Guid.CreateVersion7();

        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(new DeleteProductCommand(missing)));

        Assert.Empty(Entries(missing.ToString()));
    }

    /// <summary>#128 (specs/058): translations of categories and the removal of a product's were not on the record.</summary>
    [Fact]
    public async Task Translating_a_category_and_removing_translations_are_recorded()
    {
        var id = Guid.CreateVersion7();
        var category = await SendAsync(new CreateCategoryCommand($"Tra {id:N}"[..20], null, $"tra-{id:N}"[..20], null));
        await SendAsync(new SetCategoryTranslationCommand(category.Id, "en", "Mirrorless cameras", null));
        await SendAsync(new RemoveCategoryTranslationCommand(category.Id, "en"));

        var sku = $"TRA{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Camera {sku}", null, 1_000_000m, sku, category.Id));
        await SendAsync(new SetProductTranslationCommand(product.Id, "en", "A camera", null));
        await SendAsync(new RemoveProductTranslationCommand(product.Id, "en"));

        Assert.Equal(["CategoryTranslated", "CategoryTranslationRemoved"],
            Entries(category.Id.ToString()).Where(e => e.Action != "CategoryCreated").Select(e => e.Action));
        var removed = Assert.Single(Entries(product.Id.ToString()), e => e.Action == "ProductTranslationRemoved");
        Assert.Equal("Catalog", removed.Category);
        Assert.Contains("A camera", removed.Before);
    }

    private List<AuditEntryRecorded> Entries(params string[] subjects) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>()
            .Select(x => x.Context.Message)
            .Where(m => m.SubjectId is not null && subjects.Contains(m.SubjectId))
            .OrderBy(m => m.OccurredAt)
            .ToList();

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
