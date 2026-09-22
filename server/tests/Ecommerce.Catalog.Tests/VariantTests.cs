using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A product is sold in several shapes, and the variant is the one that carries the price, the sku and
/// the stock (specs/020).
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class VariantTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task Creating_a_product_creates_its_first_variant()
    {
        var product = await CreateProductAsync(price: 1000m);

        var variant = Assert.Single(product.Variants!);
        Assert.Equal(product.Sku, variant.Sku);
        Assert.Equal(1000m, variant.Price);
        Assert.Empty(variant.Options);          // one shape: nothing to choose between
        Assert.Empty(variant.OptionSummary);
        Assert.False(product.PriceVaries);
    }

    [Fact]
    public async Task A_second_shape_gets_its_own_price_and_sku_and_the_product_shows_the_cheaper_one()
    {
        var product = await CreateProductAsync(price: 1000m);

        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 1400m, [new VariantOptionInput("Kit", "With 24-105mm")]));

        Assert.Equal("Kit: With 24-105mm", kit.OptionSummary);

        var detail = await SendAsync(new GetProductByIdQuery(product.Id));
        Assert.Equal(2, detail!.Variants!.Count);
        Assert.Equal(2, detail.VariantCount);
        Assert.True(detail.PriceVaries);
        Assert.Equal(1000m, detail.Price);       // a "from" price: the cheapest shape

        // And the listing agrees, without loading every option on the page.
        var listed = await ListAsync(product.Id);
        Assert.Equal(1000m, listed.Price);
        Assert.True(listed.PriceVaries);
        Assert.Null(listed.Variants);
    }

    [Fact]
    public async Task A_cheaper_variant_that_is_taken_off_sale_stops_setting_the_from_price()
    {
        var product = await CreateProductAsync(price: 1000m);
        var cheapest = Assert.Single(product.Variants!);
        // A second shape must be distinguishable, or the handler refuses it - which is the point.
        await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 1400m, [new VariantOptionInput("Kit", "With 24-105mm")]));

        await SendAsync(new UpdateProductVariantCommand(
            product.Id, cheapest.Id, cheapest.Price!.Value, IsActive: false));

        var detail = await SendAsync(new GetProductByIdQuery(product.Id));
        Assert.Equal(1400m, detail!.Price);
        Assert.Equal(1, detail.VariantCount);
        Assert.False(detail.PriceVaries);
        Assert.Equal(1400m, (await ListAsync(product.Id)).Price);
    }

    [Fact]
    public async Task A_sku_already_in_the_catalogue_is_refused()
    {
        var product = await CreateProductAsync(price: 10m);
        var taken = Assert.Single(product.Variants!).Sku;

        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new AddProductVariantCommand(product.Id, taken, 20m, [new VariantOptionInput("Kit", "Other")])));
    }

    [Fact]
    public async Task Two_shapes_a_customer_cannot_tell_apart_are_refused()
    {
        var product = await CreateProductAsync(price: 10m);
        await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-B", 20m, [new VariantOptionInput("Colour", "Black")]));

        // Same options, different sku - and nothing for the customer to choose between.
        var refused = await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new AddProductVariantCommand(
                product.Id, $"{product.Sku}-B2", 25m, [new VariantOptionInput("colour", " black ")])));

        Assert.Contains("already has a variant", refused.Message);
    }

    [Fact]
    public async Task Availability_is_per_variant_and_the_product_reads_as_in_stock_if_any_variant_is()
    {
        var product = await CreateProductAsync(price: 10m);
        var body = Assert.Single(product.Variants!);
        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 20m, [new VariantOptionInput("Kit", "With lens")]));
        var observedAt = DateTime.UtcNow;

        Assert.True(await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, observedAt, kit.Id)));

        var detail = await SendAsync(new GetProductByIdQuery(product.Id));
        Assert.Equal(ProductAvailability.OutOfStock, Variant(detail!, body.Id).Availability);
        Assert.Equal(ProductAvailability.InStock, Variant(detail, kit.Id).Availability);
        Assert.Equal(ProductAvailability.InStock, detail.Availability);   // any shape in stock

        // And when the only stocked shape runs out, so does the product.
        Assert.True(await SendAsync(new RecordStockAvailabilityCommand(product.Id, false, observedAt.AddSeconds(1), kit.Id)));
        Assert.Equal(ProductAvailability.OutOfStock, (await SendAsync(new GetProductByIdQuery(product.Id)))!.Availability);
    }

    [Fact]
    public async Task A_variant_of_another_product_is_not_found()
    {
        var mine = await CreateProductAsync(price: 10m);
        var theirs = await CreateProductAsync(price: 10m);
        var theirVariant = Assert.Single(theirs.Variants!);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new UpdateProductVariantCommand(mine.Id, theirVariant.Id, 11m, true)));
    }

    [Fact]
    public async Task The_migration_gives_every_existing_product_one_variant_with_the_products_own_id()
    {
        // The decision the whole feature rests on (research D2): Inventory's stock rows, Cart's lines
        // and Order's lines hold a product id, in three other databases. The backfilled variant reuses
        // that id, so all of them are already keyed by the right variant.
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        var name = $"catalog_migration_{Guid.NewGuid():N}";
        var admin = $"Host=localhost;Port=5433;Database=postgres;Username=postgres;Password={password}";
        var connection = $"Host=localhost;Port=5433;Database={name};Username=postgres;Password={password}";

        await using (var conn = new NpgsqlConnection(admin))
        {
            await conn.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn).ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options;
            await using var db = new CatalogDbContext(options);
            var migrator = db.GetService<IMigrator>();

            // The schema as it was before variants existed.
            await migrator.MigrateAsync("20260922005055_AddProductImage");

            var categoryId = Guid.CreateVersion7();
            var productId = Guid.CreateVersion7();
            // Raw, with parameters: the column names need their own quotes, and an interpolated
            // string spliced across lines is a plain string, which ExecuteSqlInterpolated will not take.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO categories (\"Id\", \"Name\", \"Slug\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\")"
                + " VALUES ({0}, 'Cameras', 'cameras', true, now(), now())", categoryId);

            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO products (\"Id\", \"Name\", \"Price\", \"Sku\", \"CategoryId\", \"IsActive\","
                + " \"Availability\", \"AvailabilityObservedAt\", \"CreatedAt\", \"UpdatedAt\")"
                + " VALUES ({0}, 'Old camera', 4999.99, 'OLD-1', {1}, true, true, now(), now(), now())",
                productId, categoryId);

            await migrator.MigrateAsync();

            var variant = await db.ProductVariants.AsNoTracking().SingleAsync(v => v.ProductId == productId);
            Assert.Equal(productId, variant.Id);          // the id is reused
            Assert.Equal("OLD-1", variant.Sku);
            Assert.Equal(4999.99m, variant.Price);
            Assert.True(variant.Availability);            // carried over, not reset
            Assert.Empty(variant.OptionSummary);
            Assert.Empty(await db.VariantOptions.AsNoTracking().Where(o => o.VariantId == variant.Id).ToListAsync());
        }
        finally
        {
            await using var conn = new NpgsqlConnection(admin);
            await conn.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn).ExecuteNonQueryAsync();
        }
    }

    private static VariantResponse Variant(ProductResponse product, Guid variantId) =>
        product.Variants!.Single(v => v.Id == variantId);

    [Fact]
    public async Task Two_shapes_of_one_product_list_their_options_in_the_same_order()
    {
        var product = await CreateProductAsync(price: 42_000_000m);
        var body = Assert.Single(product.Variants!);

        // Entered in OPPOSITE orders on purpose. Nothing orders the rows, so before this was fixed
        // the two summaries came back as "Colour: … · Kit: …" and "Kit: … · Colour: …" - two
        // different strings describing one product, which is what Summarise exists to prevent.
        // Seen in the seeded camera catalogue, not invented here.
        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id,
            $"{product.Sku}-KIT",
            52_500_000m,
            [new VariantOptionInput("Kit", "With 18-55mm"), new VariantOptionInput("Colour", "Black")]));

        var detail = await SendAsync(new GetProductByIdQuery(product.Id));
        var summaries = detail!.Variants!.Select(v => v.OptionSummary).Where(s => s.Length > 0).ToList();

        Assert.All(summaries, summary => Assert.StartsWith("Colour: ", summary));
        Assert.Equal("Colour: Black · Kit: With 18-55mm", kit.OptionSummary);
        Assert.NotEqual(body.Id, kit.Id);
    }

    [Fact]
    public async Task An_option_carries_its_id_so_it_can_be_addressed()
    {
        var product = await CreateProductAsync(price: 42_000_000m);

        var kit = await SendAsync(new AddProductVariantCommand(
            product.Id, $"{product.Sku}-KIT", 52_500_000m, [new VariantOptionInput("Kit", "With 18-55mm")]));

        // Without this the translation endpoint added in specs/021 - PUT
        // /api/products/{id}/options/{optionId}/translations/{lang} - could not be called by anything
        // outside the database, because no response carried the id it takes. Found by the camera
        // seeder, which is the first API client that ever tried to use it.
        var option = Assert.Single(kit.Options);
        Assert.NotEqual(Guid.Empty, option.Id);

        var detail = await SendAsync(new GetProductByIdQuery(product.Id));
        var read = detail!.Variants!.Single(v => v.Id == kit.Id).Options.Single();
        Assert.Equal(option.Id, read.Id);
    }

    private async Task<ProductResponse> ListAsync(Guid productId)
    {
        var page = await SendAsync(new GetProductsQuery { PageSize = 200 });
        return page.Items.Single(p => p.Id == productId);
    }

    private async Task<ProductResponse> CreateProductAsync(decimal price)
    {
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = $"Var {categoryId:N}"[..20],
                Slug = $"var-{categoryId:N}"[..20],
            });
            await context.SaveChangesAsync();
        }

        var sku = $"VAR{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Camera {sku}", null, price, sku, categoryId));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
