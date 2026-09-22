using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Catalog;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Removing a product for good, and what it must take with it (specs/024).
/// </summary>
/// <remarks>
/// This is the rare operation. Taking something off sale is deactivation, which leaves the row where
/// a cart and a report can still find it; this exists for rows that should never have existed - the
/// ninety-four <c>E2E Widget …</c> products the CI scripts had left in the catalogue.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class DeleteProductTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task A_deleted_product_is_gone_along_with_everything_that_described_it()
    {
        var product = await CreateProductAsync();
        var body = Assert.Single(product.Variants!);

        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 52_000_000m, [new VariantOptionInput("Kit", "With a lens")]));

        await SendAsync(new SetProductTranslationCommand(product.Id, "en", "A camera", "In English"));
        await SendAsync(new SetVariantPriceCommand(product.Id, kit.Id, "USD", 1999m));

        await SendAsync(new DeleteProductCommand(product.Id));

        Assert.Null(await SendAsync(new GetProductByIdQuery(product.Id)));

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // The variants go with it - their foreign key is RESTRICT precisely so this has to be said
        // on purpose - and their options, prices and translations go with them.
        Assert.False(await context.ProductVariants.AnyAsync(v => v.Id == body.Id || v.Id == kit.Id));
        Assert.False(await context.VariantOptions.AnyAsync(o => o.VariantId == kit.Id));
        Assert.False(await context.VariantPrices.AnyAsync(p => p.VariantId == kit.Id));
        Assert.False(await context.ProductTranslations.AnyAsync(t => t.ProductId == product.Id));
    }

    [Fact]
    public async Task Deleting_it_announces_every_variant_so_Inventory_can_forget_them()
    {
        var product = await CreateProductAsync();
        var body = Assert.Single(product.Variants!);

        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 52_000_000m, [new VariantOptionInput("Kit", "With a lens")]));

        await SendAsync(new DeleteProductCommand(product.Id));

        var published = _fixture.Harness.Published
            .Select<ProductDeletedEvent>()
            .Single(m => m.Context.Message.ProductId == product.Id)
            .Context.Message;

        // Every variant, not just the product: Inventory counts stock per VARIANT, so a product id
        // alone would leave the kit's units on the shelf forever.
        Assert.Equal(2, published.VariantIds.Count);
        Assert.Contains(body.Id, published.VariantIds);
        Assert.Contains(kit.Id, published.VariantIds);
    }

    [Fact]
    public async Task Deleting_a_product_that_is_not_there_is_a_404_not_a_shrug()
    {
        // Silently succeeding would let a script report that it cleaned up something it never found.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new DeleteProductCommand(Guid.CreateVersion7())));
    }

    [Fact]
    public async Task A_deleted_products_sku_can_be_used_again()
    {
        var product = await CreateProductAsync();
        var sku = product.Sku;

        await SendAsync(new DeleteProductCommand(product.Id));

        // The unique index on Sku is what would refuse this if anything had been left behind, so
        // this is the index confirming the deletion rather than a convenience being tested.
        var again = await SendAsync(new CreateProductCommand(
            "Another camera", null, 1_000_000m, sku, product.CategoryId));

        Assert.Equal(sku, again.Sku);
    }

    private async Task<ProductResponse> CreateProductAsync()
    {
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = $"Del {categoryId:N}"[..20],
                Slug = $"del-{categoryId:N}"[..20],
            });
            await context.SaveChangesAsync();
        }

        var sku = $"DEL{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Camera {sku}", null, 40_000_000m, sku, categoryId));
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
