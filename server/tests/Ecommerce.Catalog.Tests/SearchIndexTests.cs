using System.Data.Common;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// The search can use its indexes (specs/074, #113). The results are pinned by the search tests elsewhere; this
/// pins the SHAPE: the SQL EF generates is asked for its plan, with sequential scans priced out, and the plan must
/// use the products' two trigram indexes, match translations with a LIKE over <c>f_unaccent</c>, and scan no table
/// whole.
/// </summary>
/// <remarks>
/// The table here is tiny, so on its own the planner would rightly scan it; <c>enable_seqscan = off</c> asks what it
/// COULD do instead. Each regression this guards against leaves a plan with no index in it: a <c>strpos</c> instead
/// of a LIKE, <c>unaccent</c> instead of the IMMUTABLE <c>f_unaccent</c> the indexes are built over, or the OR with a
/// correlated EXISTS that kept a sequential scan of products even with the indexes present (457 ms on 100,000).
/// </remarks>
[Collection(nameof(CatalogTestCollection))]
public class SearchIndexTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task The_search_query_is_one_its_trigram_indexes_can_answer()
    {
        string connection;
        await using (var scope = _fixture.NewScope())
        {
            connection = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.GetConnectionString()!;
        }

        var explain = new ExplainTheSearch();
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).AddInterceptors(explain).Options;
        await using var context = new CatalogDbContext(options);

        await new ProductRepository(context).GetPaginatedAsync(1, 12, null, "may anh", null, language: "vi");

        var plan = Assert.Single(explain.Plans);
        Assert.Contains("IX_products_name_search", plan);
        Assert.Contains("IX_products_sku_search", plan);
        Assert.DoesNotContain("Seq Scan", plan);
        // ...and no SubPlan: the OR-with-EXISTS shape runs the translations' match as a (hashed) subplan per product,
        // which is what kept every product scanned. The UNION of ids is a join instead.
        Assert.DoesNotContain("SubPlan", plan);
        // The translations' arm is a LIKE over f_unaccent too - on a table this small the planner reaches it through
        // the (ProductId, Language) index and filters by name; on 100,000 rows it uses its own trigram index.
        Assert.Contains("f_unaccent(lower((\"Name\")::text)) ~~", plan);
        Assert.DoesNotContain("strpos", plan);
    }

    /// <summary>Before the search's count query runs, asks PostgreSQL for its plan with sequential scans priced out.</summary>
    private sealed class ExplainTheSearch : DbCommandInterceptor
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
            if (!command.CommandText.Contains("f_unaccent") || !command.CommandText.TrimStart().StartsWith("SELECT count(*)"))
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
                on.CommandText = "RESET enable_seqscan";
                await on.ExecuteNonQueryAsync(cancellationToken);
            }

            Plans.Add(string.Join('\n', lines));
        }
    }
}
