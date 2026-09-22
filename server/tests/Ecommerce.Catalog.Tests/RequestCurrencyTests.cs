using Ecommerce.Shared.Money;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// How a request says which currency it wants, and what it gets when it says nothing (specs/022).
/// </summary>
/// <remarks>
/// These build a pipeline by hand rather than going through a service, because what is under test is
/// the resolution itself - <c>?currency=</c>, then <c>X-Currency</c>, then the configured default -
/// and the header that tells a cache the answer depended on being asked.
/// </remarks>
public class RequestCurrencyTests
{
    [Theory]
    // The query string wins, so a link can carry the currency its prices were quoted in.
    [InlineData("?currency=USD", null, "USD")]
    [InlineData("?currency=usd", null, "USD")]          // a code is a code, whatever its case
    [InlineData("?currency=USD", "VND", "USD")]
    // ...then the header, which is what the storefront sends on every call.
    [InlineData("", "USD", "USD")]
    [InlineData("", "vnd", "VND")]
    // ...then the default.
    [InlineData("", null, "VND")]
    [InlineData("", "", "VND")]
    // A currency the shop does not price in falls back rather than 404s: the shopper asked for
    // something reasonable and the shop simply does not offer it.
    [InlineData("?currency=JPY", null, "VND")]
    [InlineData("", "JPY", "VND")]
    [InlineData("?currency=notacurrency", null, "VND")]
    public async Task The_currency_is_resolved_in_order(string query, string? header, string expected)
    {
        var resolved = await ResolveAsync(query, header, language: null);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public async Task The_currency_is_not_taken_from_the_language()
    {
        // The cheap wrong answer: English means dollars, Vietnamese means dong. Most of this shop's
        // customers are Vietnamese people reading English, and they pay in dong.
        Assert.Equal("VND", await ResolveAsync("", header: null, language: "en"));
        Assert.Equal("VND", await ResolveAsync("", header: null, language: "en-US"));

        // ...and the other direction: reading Vietnamese does not stop somebody paying in dollars.
        Assert.Equal("USD", await ResolveAsync("", header: "USD", language: "vi"));
    }

    [Fact]
    public async Task The_response_says_which_currency_it_is_in_and_that_it_depended_on_being_asked()
    {
        var context = await RunAsync("?currency=USD", header: null, language: null);

        Assert.Equal("USD", context.Response.Headers["X-Currency"]);

        // Without Vary, a cache between here and the customer may serve one shopper's dong prices to
        // the next shopper asking in dollars, because the URL is identical.
        Assert.Contains("X-Currency", context.Response.Headers.Vary.ToString());
    }

    [Fact]
    public void A_default_currency_that_is_not_supported_is_refused_at_startup()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Money:DefaultCurrency"] = "JPY",
                ["Money:Supported:0:Code"] = "VND",
                ["Money:Supported:0:Decimals"] = "0",
            })
            .Build();

        // Every request would fall back to a currency nothing can price in. Better a service that
        // refuses to start than one that reports healthy and answers wrongly.
        var failure = Assert.Throws<InvalidOperationException>(
            () => services.AddRequestCurrency(configuration));

        Assert.Contains("JPY", failure.Message);
    }

    [Fact]
    public void A_currency_configured_twice_is_refused_at_startup()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Money:DefaultCurrency"] = "VND",
                ["Money:Supported:0:Code"] = "VND",
                ["Money:Supported:0:Decimals"] = "0",
                ["Money:Supported:1:Code"] = "vnd",
                ["Money:Supported:1:Decimals"] = "2",
            })
            .Build();

        // This is the check that caught the configuration binder APPENDING to a defaulted array
        // rather than replacing it - the reason neither options type has a default any more.
        var failure = Assert.Throws<InvalidOperationException>(
            () => services.AddRequestCurrency(configuration));

        Assert.Contains("twice", failure.Message);
    }

    [Fact]
    public void Dong_has_no_decimal_places_and_dollars_have_two()
    {
        // Not decoration: OrderTotals rounds tax to this, and a shop that rounds every currency to
        // two produces totals like 30.000,50 dong, which is not a price anybody can pay.
        Assert.Equal(3_703_703m, new Currency("VND", 0).Round(3_703_703.4m));
        Assert.Equal(3.70m, new Currency("USD", 2).Round(3.7037m));

        // Halves away from zero, like every other amount in this system - not .NET's banker's
        // rounding, which surprises anyone checking a receipt by hand.
        Assert.Equal(3m, new Currency("VND", 0).Round(2.5m));
        Assert.Equal(0.13m, new Currency("USD", 2).Round(0.125m));
    }

    private static async Task<string> ResolveAsync(string query, string? header, string? language)
    {
        var context = await RunAsync(query, header, language);
        return context.Response.Headers["X-Currency"].ToString();
    }

    private static async Task<HttpContext> RunAsync(string query, string? header, string? language)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Money:DefaultCurrency"] = "VND",
            ["Money:Supported:0:Code"] = "VND",
            ["Money:Supported:0:Decimals"] = "0",
            ["Money:Supported:1:Code"] = "USD",
            ["Money:Supported:1:Decimals"] = "2",
        });

        builder.Services.AddRequestCurrency(builder.Configuration);

        var app = builder.Build();
        app.UseRequestCurrency();
        app.Run(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        context.Request.Path = "/api/products";
        context.Request.QueryString = new QueryString(query);

        if (header is not null)
        {
            context.Request.Headers["X-Currency"] = header;
        }

        if (language is not null)
        {
            context.Request.Headers.AcceptLanguage = language;
        }

        // The resolver reads HttpContext through the accessor, exactly as it does behind a real
        // request; a hand-built pipeline has to set it or every answer is the default.
        context.RequestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        await ((IApplicationBuilder)app).Build().Invoke(context);

        return context;
    }
}
