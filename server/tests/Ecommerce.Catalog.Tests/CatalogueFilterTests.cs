using System.Data.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.Infrastructure.Persistence.Repositories;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// The catalogue filtered by price and by what is in stock (#216, specs/109): a range of the "from" price the card
/// shows, in the currency asked for and never converted, and only what can be bought now - answered by indexes.
/// Each test lists one category of its own, so the shared database's other products do not count.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class CatalogueFilterTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;
    private static readonly Currency Dong = new("VND", 0);
    private static readonly Currency Dollars = new("USD", 2);

    [Fact]
    public async Task A_price_range_in_dong_keeps_what_its_from_price_falls_in_both_ends_included()
    {
        var category = await CategoryAsync();
        var cheap = await CreateAsync(category, 10_000_000m);
        var middle = await CreateAsync(category, 20_000_000m);
        var dear = await CreateAsync(category, 30_000_000m);

        Assert.Equal([cheap, middle], await ListAsync(new GetProductsQuery(CategoryId: category, MaxPrice: 20_000_000m)));
        Assert.Equal([middle, dear], await ListAsync(new GetProductsQuery(CategoryId: category, MinPrice: 20_000_000m)));
        Assert.Equal([middle], await ListAsync(new GetProductsQuery(CategoryId: category, MinPrice: 15_000_000m, MaxPrice: 25_000_000m)));
        Assert.Equal([cheap, middle, dear], await ListAsync(new GetProductsQuery(CategoryId: category)));
    }

    [Fact]
    public async Task A_range_in_dollars_reads_the_dollar_prices_and_drops_what_has_none()
    {
        var category = await CategoryAsync();
        // Dear in dong, cheap in dollars - and the other way round: nothing is converted (specs/022).
        var a = await CreateAsync(category, 40_000_000m, usd: 500m);
        var b = await CreateAsync(category, 10_000_000m, usd: 1500m);
        await CreateAsync(category, 5_000_000m);   // no dollar price at all

        Assert.Equal([a], await ListAsync(new GetProductsQuery(CategoryId: category, MaxPrice: 1000m), Dollars));
        Assert.Equal([b], await ListAsync(new GetProductsQuery(CategoryId: category, MinPrice: 1000m), Dollars));
        Assert.Equal([a, b], await ListAsync(new GetProductsQuery(CategoryId: category, MinPrice: 0m), Dollars));
    }

    [Fact]
    public async Task A_withdrawn_variant_s_price_is_not_the_from_price()
    {
        var category = await CategoryAsync();
        var product = await CreateAsync(category, 30_000_000m, usd: 1500m);
        var kit = await SendAsync(new AddProductVariantCommand(
            product, $"KIT{Guid.NewGuid():N}"[..20], 50_000_000m, [new VariantOptionInput("Kit", "With lens")]));
        await SendAsync(new SetVariantPriceCommand(product, kit.Id, "USD", 900m));
        Assert.Equal([product], await ListAsync(new GetProductsQuery(CategoryId: category, MaxPrice: 1000m), Dollars));

        await SendAsync(new UpdateProductVariantCommand(product, kit.Id, 50_000_000m, IsActive: false));

        Assert.Empty(await ListAsync(new GetProductsQuery(CategoryId: category, MaxPrice: 1000m), Dollars));
    }

    [Fact]
    public async Task In_stock_only_leaves_out_what_cannot_be_bought_now()
    {
        var category = await CategoryAsync();
        var inStock = await CreateAsync(category, 10_000_000m);
        var soldOut = await CreateAsync(category, 11_000_000m);
        await SendAsync(new RecordStockAvailabilityCommand(inStock, true, DateTime.UtcNow, inStock));

        Assert.Equal([inStock], await ListAsync(new GetProductsQuery(CategoryId: category, InStock: true)));
        Assert.Equal([inStock, soldOut], await ListAsync(new GetProductsQuery(CategoryId: category)));
    }

    [Fact]
    public async Task A_reversed_or_negative_range_is_refused()
    {
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetProductsQuery(MinPrice: 5m, MaxPrice: 1m)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetProductsQuery(MinPrice: -1m)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetProductsQuery(MaxPrice: -1m)));
    }

    [Fact]
    public async Task A_range_in_dong_is_answered_by_the_price_index()
    {
        var plan = await PlanAsync("VND", new ProductFilter(MaxPrice: 20_000_000m));

        Assert.True(plan.Contains("IX_products_on_shelf_Price"), plan);
        Assert.DoesNotContain("SubPlan", plan);
    }

    [Fact]
    public async Task A_range_in_dollars_is_a_join_over_one_currency_s_prices_never_a_subplan_per_product()
    {
        var plan = await PlanAsync("USD", new ProductFilter(MinPrice: 100m, MaxPrice: 1000m));

        Assert.Contains("IX_variant_prices_Currency_Amount", plan);
        // The sort's correlated MIN per product would be a SubPlan run for every product (specs/074's 457 ms).
        Assert.DoesNotContain("SubPlan", plan);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<List<Guid>> ListAsync(GetProductsQuery query, Currency? currency = null) =>
        (await SendAsync(query with { PageSize = 50, SortBy = "price_asc" }, currency)).Items
            .OrderBy(p => p.Price).Select(p => p.Id).ToList();

    private async Task<Guid> CategoryAsync()
    {
        var id = Guid.CreateVersion7();
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.Categories.Add(new Category { Id = id, Name = $"Flt {id:N}"[..20], Slug = $"flt-{id:N}"[..20] });
        await context.SaveChangesAsync();
        return id;
    }

    /// <summary>The shop's own product - approved as listed - priced in dong, and in dollars when asked.</summary>
    private async Task<Guid> CreateAsync(Guid category, decimal dong, decimal? usd = null)
    {
        var sku = $"FLT{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Filter {sku}", null, dong, sku, category));
        if (usd is { } dollars)
            await SendAsync(new SetVariantPriceCommand(product.Id, product.Id, "USD", dollars));
        return product.Id;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request, Currency? currency = null)
    {
        await using var scope = _fixture.NewScope(currency: currency ?? Dong);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request, Currency? currency = null)
    {
        await using var scope = _fixture.NewScope(currency: currency ?? Dong);
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>The count query's plan with sequential scans priced out, as specs/074's search test asks it.</summary>
    private async Task<string> PlanAsync(string currency, ProductFilter filter)
    {
        string connection;
        await using (var scope = _fixture.NewScope())
        {
            connection = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.GetConnectionString()!;
        }

        var explain = new ExplainTheCount();
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).AddInterceptors(explain).Options;
        await using var context = new CatalogDbContext(options);
        await new ProductRepository(context).GetPaginatedAsync(1, 12, null, null, null,
            currency: currency, defaultCurrency: "VND", filter: filter);
        return Assert.Single(explain.Plans);
    }

    private sealed class ExplainTheCount : DbCommandInterceptor
    {
        public List<string> Plans { get; } = [];

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            await ExplainAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await ExplainAsync(command, cancellationToken);
            return result;
        }

        private async Task ExplainAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (!command.CommandText.TrimStart().StartsWith("SELECT count(*)"))
                return;

            await using (var off = command.Connection!.CreateCommand())
            {
                off.CommandText = "SET enable_seqscan = off";
                await off.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var explain = command.Connection!.CreateCommand();
            explain.CommandText = "EXPLAIN " + command.CommandText;
            foreach (DbParameter parameter in command.Parameters)
            {
                var copy = explain.CreateParameter();
                copy.ParameterName = parameter.ParameterName;
                copy.Value = parameter.Value;
                copy.DbType = parameter.DbType;
                explain.Parameters.Add(copy);
            }

            var lines = new List<string>();
            await using (var reader = await explain.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    lines.Add(reader.GetString(0));
            }

            await using (var on = command.Connection!.CreateCommand())
            {
                on.CommandText = "SET enable_seqscan = on";
                await on.ExecuteNonQueryAsync(cancellationToken);
            }

            Plans.Add(string.Join('\n', lines));
        }
    }
}
