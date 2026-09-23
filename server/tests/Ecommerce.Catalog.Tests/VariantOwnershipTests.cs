using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// What Catalog answers when Inventory asks who owns a variant (specs/031).
/// </summary>
/// <remarks>
/// <para>
/// Inventory holds stock keyed by variant id and has no idea who anybody is; ownership lives in
/// <c>products.SellerId</c>, here. This is the projection behind <c>CatalogOwnership</c>, and the
/// three answers it can give are each a different refusal on the other side of the wire.
/// </para>
/// <para>
/// ⚠️ <b>Absence is the answer for a variant that does not exist</b> — not an error and not a row
/// with nulls. The caller turns absence into its own 404, which is what keeps "no such variant" and
/// "not yours" worded identically where a seller can see them.
/// </para>
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class VariantOwnershipTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    /// <summary>The caller is a singleton for the whole collection; put it back (specs/027).</summary>
    public void Dispose()
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_sellers_variant_reports_the_seller()
    {
        var alice = Guid.CreateVersion7();
        var product = await AsSeller(alice, CreateProductAsync);

        var owners = await OwnersOfAsync(product.Id);

        var owner = Assert.Single(owners);
        Assert.Equal(alice, owner.SellerId);
        Assert.Equal(product.Id, owner.VariantId);
    }

    /// <summary>
    /// Null is a real state, not a missing one: every product from before specs/027, and anything an
    /// administrator lists, belongs to the shop itself. Inventory turns that into a refusal for a
    /// seller and a pass for an administrator.
    /// </summary>
    [Fact]
    public async Task The_shops_own_variant_reports_no_seller()
    {
        var product = await CreateProductAsync();       // created as an administrator

        var owners = await OwnersOfAsync(product.Id);

        Assert.Null(Assert.Single(owners).SellerId);
    }

    [Fact]
    public async Task A_variant_that_does_not_exist_is_absent_rather_than_an_error()
    {
        var owners = await OwnersOfAsync(Guid.CreateVersion7());

        Assert.Empty(owners);
    }

    /// <summary>
    /// Asked plural for one caller, the lesson PriceVariants already learned. A mixed request must
    /// answer for what exists without refusing the rest.
    /// </summary>
    [Fact]
    public async Task A_mixed_request_answers_for_what_exists()
    {
        var alice = Guid.CreateVersion7();
        var hers = await AsSeller(alice, CreateProductAsync);
        var missing = Guid.CreateVersion7();

        var owners = await OwnersOfAsync(hers.Id, missing);

        Assert.Equal(hers.Id, Assert.Single(owners).VariantId);
    }

    private async Task<List<VariantOwnership>> OwnersOfAsync(params Guid[] variantIds)
    {
        await using var scope = _fixture.NewScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        return await products.GetVariantOwnersAsync(variantIds);
    }

    private async Task<ProductResponse> CreateProductAsync()
    {
        await using var scope = _fixture.NewScope();
        var categories = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new Ecommerce.Catalog.Application.Categories.Queries.GetCategories.GetCategoriesQuery());

        var categoryId = categories.Count > 0
            ? categories[0].Id
            : (await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new Ecommerce.Catalog.Application.Categories.Commands.CreateCategory.CreateCategoryCommand(
                    $"Ownership {Guid.NewGuid():N}"[..24],
                    null,
                    $"ownership-{Guid.NewGuid():N}"[..24],
                    null))).Id;

        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateProductCommand(
                $"Ownership camera {Guid.NewGuid():N}"[..28],
                null,
                1_000_000m,
                $"OWN-{Guid.NewGuid():N}"[..16],
                categoryId));
    }

    private async Task<T> AsSeller<T>(Guid sellerId, Func<Task<T>> body)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = sellerId;
        caller.Roles.Clear();
        caller.Roles.Add("Seller");
        return await body();
    }
}
