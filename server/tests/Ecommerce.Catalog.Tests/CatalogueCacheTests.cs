using System.Net;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.WebApi.Caching;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// The catalogue's public reads, answered from memory (specs/157, #361) - through the same registration and the same
/// middleware order as Catalog's Program.cs, on a real HTTP pipeline, with a handler that counts how often it is reached.
/// </summary>
public class CatalogueCacheTests
{
    private sealed class Hits
    {
        private int _count;
        public int Count => _count;
        public int Next() => Interlocked.Increment(ref _count);
    }

    private static async Task<(WebApplication App, HttpClient Client, Hits Hits)> StartAsync(string? seconds = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Localization:DefaultLanguage"] = "vi",
            ["Localization:Supported:0"] = "vi",
            ["Localization:Supported:1"] = "en",
            ["Money:DefaultCurrency"] = "VND",
            ["Money:Supported:0:Code"] = "VND",
            ["Money:Supported:0:Decimals"] = "0",
            ["Money:Supported:1:Code"] = "USD",
            ["Money:Supported:1:Decimals"] = "2",
            [CatalogueCache.Setting] = seconds,
        });
        builder.Services.AddRequestLanguage(builder.Configuration);
        builder.Services.AddRequestCurrency(builder.Configuration);
        builder.Services.AddCatalogueCache(builder.Configuration);
        builder.Services.AddSingleton<Hits>();

        var app = builder.Build();
        app.UseRequestLanguage();
        app.UseRequestCurrency();
        app.UseOutputCache();

        // What the handler answers says how many times it was reached, and in what language and currency it answered.
        app.MapGet("/api/products", (Hits hits, IRequestLanguage language, IRequestCurrency currency) =>
                $"{hits.Next()}:{language.Current}:{currency.Current.Code}")
            .CacheOutput(CatalogueCache.Policy);
        app.MapGet("/api/products/missing", (Hits hits) => { hits.Next(); return Results.NotFound(); })
            .CacheOutput(CatalogueCache.Policy);

        await app.StartAsync();
        return (app, app.GetTestClient(), app.Services.GetRequiredService<Hits>());
    }

    private static async Task<string> GetAsync(HttpClient client, string path, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        configure?.Invoke(request);
        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task A_repeated_anonymous_read_is_answered_from_memory_with_its_headers()
    {
        var (app, client, hits) = await StartAsync();
        await using var _ = app;

        Assert.Equal("1:vi:VND", await GetAsync(client, "/api/products?pageSize=12"));

        using var again = await client.GetAsync("/api/products?pageSize=12");
        Assert.Equal("1:vi:VND", await again.Content.ReadAsStringAsync());
        Assert.Equal(1, hits.Count);

        // The replay says what it is, as the original did (specs/021, specs/022).
        Assert.Equal("vi", again.Content.Headers.ContentLanguage.Single());
        Assert.Equal("VND", again.Headers.GetValues("X-Currency").Single());
        Assert.Contains("Accept-Language", again.Headers.Vary);
        Assert.Contains("X-Currency", again.Headers.Vary);
    }

    [Fact]
    public async Task Another_language_currency_or_query_is_another_answer()
    {
        var (app, client, hits) = await StartAsync();
        await using var _ = app;

        Assert.Equal("1:vi:VND", await GetAsync(client, "/api/products"));
        Assert.Equal("2:en:VND", await GetAsync(client, "/api/products", r => r.Headers.Add("Accept-Language", "en")));
        Assert.Equal("3:vi:USD", await GetAsync(client, "/api/products", r => r.Headers.Add("X-Currency", "USD")));
        Assert.Equal("4:en:VND", await GetAsync(client, "/api/products?lang=en"));
        Assert.Equal("5:vi:VND", await GetAsync(client, "/api/products?search=shoes"));

        // Kept apart by the NEGOTIATED language: en-GB and a weighted list both mean English, one entry.
        Assert.Equal("2:en:VND", await GetAsync(client, "/api/products", r => r.Headers.Add("Accept-Language", "en-GB")));
        Assert.Equal("2:en:VND", await GetAsync(client, "/api/products",
            r => r.Headers.Add("Accept-Language", "en;q=0.9,vi;q=0.5")));
        Assert.Equal(5, hits.Count);
    }

    [Fact]
    public async Task A_signed_in_read_is_never_served_from_or_kept_in_the_cache()
    {
        var (app, client, hits) = await StartAsync();
        await using var _ = app;
        void Signed(HttpRequestMessage r) => r.Headers.Add("Authorization", "Bearer a-sellers-token");

        Assert.Equal("1:vi:VND", await GetAsync(client, "/api/products"));
        // Not served the anonymous answer...
        Assert.Equal("2:vi:VND", await GetAsync(client, "/api/products", Signed));
        // ...and its own answer was not kept for the next one.
        Assert.Equal("3:vi:VND", await GetAsync(client, "/api/products", Signed));
        Assert.Equal("1:vi:VND", await GetAsync(client, "/api/products"));
    }

    [Fact]
    public async Task Only_a_200_is_kept()
    {
        var (app, client, hits) = await StartAsync();
        await using var _ = app;

        // An off-shelf product is a 404 for anybody but its seller and staff; asked again, it is asked again.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/products/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/products/missing")).StatusCode);
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public async Task Eviction_empties_it()
    {
        var (app, client, hits) = await StartAsync();
        await using var _ = app;

        Assert.Equal("1:vi:VND", await GetAsync(client, "/api/products"));
        Assert.Equal("2:en:VND", await GetAsync(client, "/api/products?lang=en"));

        // What CatalogueWrites calls after a committed catalogue write.
        await app.Services.GetRequiredService<ICatalogueReadCache>().EvictAsync();

        Assert.Equal("3:vi:VND", await GetAsync(client, "/api/products"));
        Assert.Equal("4:en:VND", await GetAsync(client, "/api/products?lang=en"));
    }

    [Fact]
    public async Task Zero_switches_it_off()
    {
        var (app, client, hits) = await StartAsync(seconds: "0");
        await using var _ = app;

        Assert.Equal("1:vi:VND", await GetAsync(client, "/api/products"));
        Assert.Equal("2:vi:VND", await GetAsync(client, "/api/products"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("601")]
    [InlineData("half a minute")]
    public void A_setting_out_of_range_stops_the_start(string seconds)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CatalogueCache.Setting] = seconds })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => CatalogueCache.Seconds(configuration));
        Assert.Contains(CatalogueCache.Setting, error.Message);
    }

    [Fact]
    public void Unset_is_thirty_seconds()
    {
        Assert.Equal(30, CatalogueCache.Seconds(new ConfigurationBuilder().Build()));
    }
}
