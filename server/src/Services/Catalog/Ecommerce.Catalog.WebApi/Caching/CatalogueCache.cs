using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Microsoft.AspNetCore.OutputCaching;

namespace Ecommerce.Catalog.WebApi.Caching;

/// <summary>
/// The catalogue's public reads, answered from memory (specs/157, #361): the listing, a product, the categories and a
/// shop page, for anonymous callers.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole answer is kept</b> - ASP.NET Core's output cache - so a replay carries the same <c>Content-Language</c>,
/// <c>X-Currency</c> and <c>Vary</c> as the original. Its default policy is kept too, and it is what refuses the
/// dangerous cases: a request carrying <c>Authorization</c> (a seller reading their own off-shelf product, staff) is
/// neither served from nor stored in it, and only a 200 is stored, so an off-shelf product's 404 is asked every time.
/// </para>
/// <para>
/// <b>Kept apart by the NEGOTIATED language and currency</b>, plus every query key - not by the raw
/// <c>Accept-Language</c>, which would make <c>en</c>, <c>en-GB</c> and <c>en;q=0.9,vi;q=0.8</c> three entries of one
/// answer. Never one shopper's Vietnamese or dong for another's English or dollars (specs/021, specs/022).
/// </para>
/// <para>
/// <b>Emptied after every committed catalogue write</b> by <c>CatalogueWrites</c>, through <see cref="Tag"/>. The expiry
/// bounds what that cannot reach: another Catalog instance's memory.
/// </para>
/// <para>
/// <b>A read that overlapped an eviction keeps nothing.</b> Every eviction advances a generation first; a request notes
/// the generation it began under, and its answer is stored only if none has passed since. Without it, a read that
/// loaded the rows before a commit and stored its answer just after the eviction served the old rows for the whole
/// expiry - the browser flows found it (#366): a seller stocked a product and the shop said "out of stock" for 30 s,
/// because the product read now made more queries and the window widened. What is left is the instant between the
/// response's headers and its body being stored, with no database work in it.
/// </para>
/// </remarks>
public static class CatalogueCache
{
    public const string Policy = "catalogue";
    public const string Tag = "catalogue";
    public const string Setting = "Caching:CatalogueSeconds";
    public const int DefaultSeconds = 30;
    public const int MaxSeconds = 600;

    /// <summary>The expiry in seconds, 0 for off. A value that is not a whole number from 0 to 600 stops Catalog at start.</summary>
    public static int Seconds(IConfiguration configuration)
    {
        var raw = configuration[Setting];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultSeconds;
        }

        if (!int.TryParse(raw, out var seconds) || seconds < 0 || seconds > MaxSeconds)
        {
            throw new InvalidOperationException(
                $"{Setting} must be a whole number of seconds from 0 (off) to {MaxSeconds}, not '{raw}'.");
        }

        return seconds;
    }

    public static IServiceCollection AddCatalogueCache(this IServiceCollection services, IConfiguration configuration)
    {
        var seconds = Seconds(configuration);

        services.AddOutputCache(options =>
            options.AddPolicy(Policy, policy =>
            {
                if (seconds == 0)
                {
                    policy.NoCache();
                    return;
                }

                policy.Expire(TimeSpan.FromSeconds(seconds))
                    .Tag(Tag)
                    .SetVaryByQuery("*")
                    .VaryByValue(context => new KeyValuePair<string, string>(
                        "language", context.RequestServices.GetRequiredService<IRequestLanguage>().Current))
                    .VaryByValue(context => new KeyValuePair<string, string>(
                        "currency", context.RequestServices.GetRequiredService<IRequestCurrency>().Current.Code))
                    .AddPolicy<StoreOnlyIfNothingWasEvicted>();
            }));

        // Replaces the empty one Infrastructure registers, whichever was added first.
        services.AddSingleton<Generation>();
        services.AddSingleton<ICatalogueReadCache, OutputCatalogueReadCache>();
        return services;
    }

    /// <summary>How many evictions have begun. Advanced before the store is emptied, never after.</summary>
    private sealed class Generation
    {
        private long _value;

        public long Current => Interlocked.Read(ref _value);

        public void Advance() => Interlocked.Increment(ref _value);
    }

    private sealed class OutputCatalogueReadCache(IOutputCacheStore store, Generation generation) : ICatalogueReadCache
    {
        private readonly IOutputCacheStore _store = store;
        private readonly Generation _generation = generation;

        public ValueTask EvictAsync(CancellationToken cancellationToken = default)
        {
            _generation.Advance();
            return _store.EvictByTagAsync(Tag, cancellationToken);
        }
    }

    /// <summary>Stores an answer only when no eviction began while it was being made.</summary>
    private sealed class StoreOnlyIfNothingWasEvicted : IOutputCachePolicy
    {
        private static readonly object Began = new();

        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            context.HttpContext.Items[Began] = Of(context).Current;
            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
            ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            if (context.HttpContext.Items[Began] is not long began || began != Of(context).Current)
            {
                context.AllowCacheStorage = false;
            }

            return ValueTask.CompletedTask;
        }

        private static Generation Of(OutputCacheContext context) =>
            context.HttpContext.RequestServices.GetRequiredService<Generation>();
    }
}
