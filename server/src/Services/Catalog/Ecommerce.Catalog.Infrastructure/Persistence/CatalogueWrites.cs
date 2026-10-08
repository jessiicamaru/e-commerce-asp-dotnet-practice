using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

/// <summary>
/// Empties the catalogue's read cache after every committed write to the catalogue's own tables (specs/157, #361).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an interceptor and not a call in each handler.</b> Those tables are written by some forty command handlers, a
/// dozen consumers (availability from Inventory, shop names, suspensions) and many guarded SQL statements. An eviction
/// call per handler is missed by the next handler somebody writes, and nothing would say so: the page would simply be
/// stale for the cache's expiry. Every one of those paths sends its statement through this context, so this sees them
/// all.
/// </para>
/// <para>
/// <b>After the commit, never before.</b> Inside a transaction the eviction waits for <c>TransactionCommitted</c> -
/// evicting before it would let a reader cache the old rows again until the expiry - and a rollback evicts nothing.
/// A statement outside a transaction is its own commit.
/// </para>
/// <para>
/// <b>Only the tables the cached reads show.</b> A product view is counted on every product page (<c>product_views</c>,
/// <c>product_viewers</c>); evicting on it would empty the cache all the time. Saved products, questions, reviews (the
/// rating the listing shows is written to <c>products</c>), reports, the outbox and the inbox are not shown by the
/// cached reads either. Names match whole words, so <c>saved_products</c> is not <c>products</c>.
/// </para>
/// </remarks>
public sealed partial class CatalogueWrites(ICatalogueReadCache cache) : DbCommandInterceptor, IDbTransactionInterceptor
{
    private readonly ICatalogueReadCache _cache = cache;

    /// <summary>Transactions that wrote the catalogue and have not ended yet.</summary>
    private readonly ConditionalWeakTable<DbTransaction, object> _pending = new();

    private static readonly object Wrote = new();

    /// <summary>
    /// <c>INSERT INTO</c>, <c>UPDATE</c> or <c>DELETE FROM</c> one of the cached reads' tables, quoted or not, with or
    /// without the schema - EF writes <c>UPDATE "products" AS p</c>, the repositories' own SQL <c>UPDATE products</c>.
    /// A CTE's <c>UPDATE</c> and an upsert's <c>INSERT INTO ... ON CONFLICT</c> are matched by the same words.
    /// </summary>
    [GeneratedRegex(
        """\b(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+(?:"?public"?\.)?"?(?:products|product_variants|variant_prices|product_translations|variant_options|variant_option_translations|categories|category_translations|sellers|category_specifications|category_specification_translations|specification_options|specification_option_translations|product_specifications)"?(?![\w"])""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex WritesTheCatalogue();

    /// <summary>Whether a command's text writes a table the cached reads show.</summary>
    public static bool Writes(string commandText) => WritesTheCatalogue().IsMatch(commandText);

    // ---- Commands: note the write, or evict at once when there is no transaction to wait for.

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Observe(command);
        return result;
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await ObserveAsync(command, cancellationToken);
        return result;
    }

    // SaveChanges inserts and updates through a reader (RETURNING), and a guarded statement may too.
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Observe(command);
        return result;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        await ObserveAsync(command, cancellationToken);
        return result;
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Observe(command);
        return result;
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        await ObserveAsync(command, cancellationToken);
        return result;
    }

    // ---- Transactions: evict once it has committed; forget it when it has not.

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (_pending.Remove(transaction))
        {
            _cache.EvictAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public async Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_pending.Remove(transaction))
        {
            await _cache.EvictAsync(cancellationToken);
        }
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        _pending.Remove(transaction);

    public Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Remove(transaction);
        return Task.CompletedTask;
    }

    private void Observe(DbCommand command)
    {
        if (!Writes(command.CommandText))
        {
            return;
        }

        if (command.Transaction is { } transaction)
        {
            _pending.AddOrUpdate(transaction, Wrote);
            return;
        }

        _cache.EvictAsync().AsTask().GetAwaiter().GetResult();
    }

    private async ValueTask ObserveAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (!Writes(command.CommandText))
        {
            return;
        }

        if (command.Transaction is { } transaction)
        {
            _pending.AddOrUpdate(transaction, Wrote);
            return;
        }

        await _cache.EvictAsync(cancellationToken);
    }
}

/// <summary>The read cache of anything that builds the context without the web host: there is nothing to empty.</summary>
public sealed class NoCatalogueReadCache : ICatalogueReadCache
{
    public ValueTask EvictAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
