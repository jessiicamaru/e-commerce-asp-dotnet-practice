using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// What the catalogue does with what Inventory tells it — including when it is told twice, and when
/// it is told things out of order.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class AvailabilityTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task An_announcement_makes_a_product_available()
    {
        var productId = await SeedProductAsync();
        var observedAt = DateTime.UtcNow;

        // Through the broker, so this covers the consumer wiring as well as the handler.
        await _fixture.Harness.Bus.Publish(
            new StockAvailabilityChangedEvent(productId, 7, true, observedAt));

        var product = await WaitForAvailabilityAsync(productId, expected: true);

        Assert.True(product.Availability);
        Assert.Equal(observedAt, product.AvailabilityObservedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_product_nobody_has_announced_reads_as_unavailable()
    {
        var productId = await SeedProductAsync();

        var product = await ReadAsync(productId);

        Assert.False(product.Availability);
        Assert.Null(product.AvailabilityObservedAt);

        // And the same through the query a shopper actually hits.
        var response = await SendAsync(new GetProductByIdQuery(productId));
        Assert.Equal(ProductAvailability.OutOfStock, response!.Availability);
    }

    [Fact]
    public async Task Ten_deliveries_of_one_announcement_record_it_once()
    {
        var productId = await SeedProductAsync();
        var observedAt = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

        var recordedCount = 0;

        for (var i = 0; i < 10; i++)
        {
            if (await SendAsync(new RecordStockAvailabilityCommand(productId, true, observedAt)))
            {
                recordedCount++;
            }
        }

        var product = await ReadAsync(productId);

        Assert.Equal(1, recordedCount);
        Assert.True(product.Availability);
        Assert.Equal(observedAt, product.AvailabilityObservedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task An_older_observation_arriving_later_does_not_win()
    {
        var productId = await SeedProductAsync();

        var newer = new DateTime(2026, 9, 17, 10, 0, 7, DateTimeKind.Utc);
        var older = new DateTime(2026, 9, 17, 10, 0, 5, DateTimeKind.Utc);

        // The newer observation says the product is gone.
        Assert.True(await SendAsync(new RecordStockAvailabilityCommand(productId, false, newer)));

        // An older one, overtaken in flight, says it is available. It must lose — otherwise the
        // listing offers goods that are gone, which is the expensive direction of this bug.
        Assert.False(await SendAsync(new RecordStockAvailabilityCommand(productId, true, older)));

        var product = await ReadAsync(productId);

        Assert.False(product.Availability);
        Assert.Equal(newer, product.AvailabilityObservedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_newer_observation_does_win()
    {
        // The control for the test above: the guard must not simply reject everything.
        var productId = await SeedProductAsync();

        var first = new DateTime(2026, 9, 17, 11, 0, 0, DateTimeKind.Utc);
        var second = first.AddSeconds(3);

        Assert.True(await SendAsync(new RecordStockAvailabilityCommand(productId, true, first)));
        Assert.True(await SendAsync(new RecordStockAvailabilityCommand(productId, false, second)));

        var product = await ReadAsync(productId);

        Assert.False(product.Availability);
        Assert.Equal(second, product.AvailabilityObservedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task An_announcement_for_a_product_this_catalogue_does_not_hold_is_discarded()
    {
        // No exception, and no claim to have recorded anything. Throwing would make the broker
        // redeliver a message about a product that is never going to appear.
        Assert.False(await SendAsync(
            new RecordStockAvailabilityCommand(Guid.CreateVersion7(), true, DateTime.UtcNow)));
    }

    [Fact]
    public async Task Creating_a_product_does_not_accept_a_stock_quantity()
    {
        // The compile-time half of the guarantee: CreateProductCommand has no such parameter, so
        // this test exists to fail loudly if anyone puts it back. The HTTP half - a request
        // carrying the field being rejected rather than ignored - is covered by
        // JsonUnmappedMemberHandling.Disallow in Program.cs and by quickstart scenario 1.
        var properties = typeof(CreateProductCommand).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("StockQuantity", properties);
        Assert.DoesNotContain("StockQuantity", typeof(ProductResponse).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task A_newly_created_product_reads_as_out_of_stock()
    {
        var categoryId = await SeedCategoryAsync();

        var created = await SendAsync(new CreateProductCommand(
            $"Widget {Guid.NewGuid():N}"[..20],
            "created by a test",
            19.99m,
            $"SKU{Guid.NewGuid():N}"[..20],
            categoryId));

        Assert.Equal(ProductAvailability.OutOfStock, created.Availability);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Guid> SeedCategoryAsync()
    {
        var categoryId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        context.Categories.Add(new Category
        {
            Id = categoryId,
            Name = $"Category {categoryId:N}"[..20],
            Slug = $"cat-{categoryId:N}"[..20]
        });

        await context.SaveChangesAsync();

        return categoryId;
    }

    private async Task<Guid> SeedProductAsync()
    {
        var categoryId = await SeedCategoryAsync();
        var productId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        context.Products.Add(new Product
        {
            Id = productId,
            Name = $"Product {productId:N}"[..20],
            Price = 19.99m,
            Sku = $"SKU{productId:N}"[..20],
            CategoryId = categoryId
        });

        await context.SaveChangesAsync();

        return productId;
    }

    private async Task<Product> ReadAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    /// <summary>
    /// Polls the row rather than asking the harness whether it consumed anything.
    /// <c>Harness.Consumed.Any&lt;T&gt;()</c> waits on an inactivity token scoped to a per-test
    /// harness; with a fixture shared across a collection it has usually elapsed by the time the
    /// later tests run and returns <c>false</c> for messages consumed perfectly well. That cost two
    /// red tests in feature 003. The row is also the better assertion.
    /// </summary>
    private async Task<Product> WaitForAvailabilityAsync(Guid productId, bool expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var product = await ReadAsync(productId);

            if (product.Availability == expected)
            {
                return product;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Product {productId} never became {(expected ? "available" : "unavailable")} within 15 "
            + "seconds. The consumer is probably not registered on a receive endpoint.");
    }

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
