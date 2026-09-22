using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Removing a category nothing is filed under (specs/024).
/// </summary>
/// <remarks>
/// The companion to deleting a product. The test scripts create a category on every run and none of
/// them clean up, so the catalogue had 95 of them against two real ones - and unlike a junk product,
/// a junk category shows up in the filter a shopper actually uses.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class DeleteCategoryTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task An_empty_category_can_be_removed()
    {
        var category = await CreateCategoryAsync();

        await SendAsync(new DeleteCategoryCommand(category.Id));

        var listed = await SendAsync(new GetCategoriesQuery());
        Assert.DoesNotContain(listed, c => c.Id == category.Id);
    }

    [Fact]
    public async Task A_category_with_products_in_it_is_refused_and_says_how_many()
    {
        var category = await CreateCategoryAsync();
        var sku = $"CAT{Guid.NewGuid():N}"[..20];
        await SendAsync(new CreateProductCommand($"Camera {sku}", null, 1_000_000m, sku, category.Id));

        // Refused, not emptied: deleting it would either orphan the products or delete them, and
        // neither is what somebody tidying a taxonomy asked for. The foreign key would refuse this
        // anyway - checking first is how the caller gets a sentence instead of a constraint error.
        var refusal = await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new DeleteCategoryCommand(category.Id)));

        Assert.Contains("1 product", refusal.Message);
        Assert.Contains(category.Name, refusal.Message);
    }

    [Fact]
    public async Task Emptying_a_category_makes_it_removable()
    {
        var category = await CreateCategoryAsync();
        var sku = $"CAT{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Camera {sku}", null, 1_000_000m, sku, category.Id));

        await SendAsync(new DeleteProductCommand(product.Id));
        await SendAsync(new DeleteCategoryCommand(category.Id));

        var listed = await SendAsync(new GetCategoriesQuery());
        Assert.DoesNotContain(listed, c => c.Id == category.Id);
    }

    [Fact]
    public async Task Deleting_a_category_that_is_not_there_is_a_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new DeleteCategoryCommand(Guid.CreateVersion7())));
    }

    private async Task<Application.Categories.Common.CategoryResponse> CreateCategoryAsync()
    {
        var id = Guid.CreateVersion7();
        return await SendAsync(new CreateCategoryCommand(
            $"Cat {id:N}"[..20], "created by a test", $"cat-{id:N}"[..20], null));
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
