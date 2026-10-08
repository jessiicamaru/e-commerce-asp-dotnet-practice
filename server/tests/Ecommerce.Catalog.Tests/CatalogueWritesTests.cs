using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// The read cache is emptied after every committed write to the catalogue's own tables, and only then (specs/157, #361) -
/// against PostgreSQL, through a <see cref="CatalogDbContext"/> carrying the interceptor as Catalog's own does.
/// </summary>
/// <remarks>
/// The statements write with <c>WHERE false</c>: a write the database really executes, inside a real transaction, that
/// changes no row - so these need no data and leave none behind, and what is under test is only which statements and
/// which transaction outcomes empty the cache.
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class CatalogueWritesTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    private sealed class CountingCache : ICatalogueReadCache
    {
        public int Evictions;

        public ValueTask EvictAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Evictions);
            return ValueTask.CompletedTask;
        }
    }

    private (CatalogDbContext Context, CountingCache Cache) Context()
    {
        string connection;
        using (var scope = _fixture.Services.CreateScope())
        {
            connection = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.GetConnectionString()!;
        }

        var cache = new CountingCache();
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(new CatalogueWrites(cache))
            .Options;
        return (new CatalogDbContext(options), cache);
    }

    [Fact]
    public async Task A_save_through_EF_empties_it_once_committed()
    {
        var (context, cache) = Context();
        await using var _ = context;

        var slug = $"cache-{Guid.NewGuid():N}";
        context.Categories.Add(new Category { Id = Guid.CreateVersion7(), Name = "Cache test", Slug = slug, IsActive = true });
        await context.SaveChangesAsync();

        Assert.Equal(1, cache.Evictions);

        context.Categories.Remove(await context.Categories.SingleAsync(c => c.Slug == slug));
        await context.SaveChangesAsync();
        Assert.Equal(2, cache.Evictions);
    }

    [Fact]
    public async Task A_guarded_statement_in_a_transaction_empties_it_after_the_commit_not_before()
    {
        var (context, cache) = Context();
        await using var _ = context;

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync("""UPDATE products SET "IsActive" = "IsActive" WHERE false""");
            await context.Database.ExecuteSqlRawAsync("""UPDATE "sellers" SET "ShopName" = "ShopName" WHERE false""");

            // Before the commit a reader would cache the old rows straight back.
            Assert.Equal(0, cache.Evictions);

            await transaction.CommitAsync();
        }

        Assert.Equal(1, cache.Evictions);
    }

    [Fact]
    public async Task A_rolled_back_write_empties_nothing()
    {
        var (context, cache) = Context();
        await using var _ = context;

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync("""UPDATE "variant_prices" SET "Amount" = "Amount" WHERE false""");
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, cache.Evictions);
    }

    [Fact]
    public async Task A_statement_outside_a_transaction_is_its_own_commit()
    {
        var (context, cache) = Context();
        await using var _ = context;

        await context.Database.ExecuteSqlRawAsync("""DELETE FROM categories WHERE false""");

        Assert.Equal(1, cache.Evictions);
    }

    [Fact]
    public async Task Counting_a_view_or_saving_a_product_empties_nothing()
    {
        var (context, cache) = Context();
        await using var _ = context;

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            // Every product page counts a view: evicting on it would empty the cache all the time.
            await context.Database.ExecuteSqlRawAsync("""UPDATE product_views SET "Views" = "Views" WHERE false""");
            await context.Database.ExecuteSqlRawAsync("""DELETE FROM saved_products WHERE false""");
            await context.Database.ExecuteSqlRawAsync("""SELECT 1 FROM products FOR UPDATE""");
            await transaction.CommitAsync();
        }

        Assert.Equal(0, cache.Evictions);
    }

    [Theory]
    [InlineData("""UPDATE "products" AS p SET "Price" = @p0 WHERE p."Id" = @p1""")]
    [InlineData("""UPDATE products SET "SellerSuspended" = true WHERE "SellerId" = @id""")]
    [InlineData("""INSERT INTO "product_variants" ("Id", "ProductId") VALUES (@p0, @p1) RETURNING "CreatedAt";""")]
    [InlineData("""INSERT INTO sellers ("Id", "Name") VALUES (@id, @name) ON CONFLICT ("Id") DO UPDATE SET "Name" = @name""")]
    [InlineData("""DELETE FROM "variant_prices" WHERE "VariantId" = @p0""")]
    [InlineData("""WITH flipped AS (UPDATE products SET "Availability" = true WHERE "Id" = @id RETURNING 1) SELECT 1""")]
    [InlineData("""INSERT INTO public.category_translations ("CategoryId") VALUES (@p0)""")]
    [InlineData("""update "variant_option_translations" set "Value" = @v""")]
    [InlineData("""INSERT INTO "product_specifications" ("ProductId", "SpecificationId", "OptionId") VALUES (@p0, @p1, @p2)""")]
    [InlineData("""DELETE FROM "specification_options" WHERE "Id" = @p0""")]
    public void A_write_to_a_table_the_cached_reads_show_is_one(string sql)
    {
        Assert.True(CatalogueWrites.Writes(sql));
    }

    [Theory]
    [InlineData("""SELECT p."Id" FROM products AS p WHERE p."Id" = @id FOR UPDATE""")]
    [InlineData("""INSERT INTO product_views ("ProductId", "Day", "Views") VALUES (@p, @d, 1) ON CONFLICT ("ProductId", "Day") DO UPDATE SET "Views" = product_views."Views" + 1""")]
    [InlineData("""INSERT INTO saved_products ("CustomerId", "ProductId") VALUES (@c, @p) ON CONFLICT DO NOTHING""")]
    [InlineData("""DELETE FROM product_viewers WHERE "Day" < @cutoff""")]
    [InlineData("""INSERT INTO "OutboxMessage" ("SequenceNumber") VALUES (@p0)""")]
    [InlineData("""UPDATE "InboxState" SET "Consumed" = @p0""")]
    [InlineData("""UPDATE product_reviews SET "Hidden" = true WHERE "Id" = @id""")]
    [InlineData("""INSERT INTO review_eligibility ("CustomerId", "ProductId") VALUES (@c, @p)""")]
    public void A_write_elsewhere_is_not(string sql)
    {
        Assert.False(CatalogueWrites.Writes(sql));
    }
}
