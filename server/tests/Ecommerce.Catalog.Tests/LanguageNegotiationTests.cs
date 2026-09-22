using System.Globalization;
using Ecommerce.Shared.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Which language a request is answered in (specs/021): <c>?lang=</c>, then <c>Accept-Language</c>,
/// then the shop's default.
/// </summary>
/// <remarks>
/// The negotiation is ASP.NET Core's <c>RequestLocalizationMiddleware</c>, configured by
/// <c>AddRequestLanguage</c>. These run it for real - the middleware against a request - rather than
/// testing a copy of its rules.
/// </remarks>
public class LanguageNegotiationTests
{
    [Fact]
    public async Task A_request_that_says_nothing_gets_the_shops_default()
    {
        Assert.Equal("vi", await NegotiateAsync());
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]           // a region tag is its language
    [InlineData("vi-VN", "vi")]
    [InlineData("de", "vi")]              // not spoken here: the default, not a 404
    [InlineData("", "vi")]
    public async Task Accept_language_decides_when_it_names_one_we_speak(string header, string expected)
    {
        Assert.Equal(expected, await NegotiateAsync(acceptLanguage: header));
    }

    [Fact]
    public async Task Quality_values_are_honoured_not_the_order_the_header_happens_to_be_in()
    {
        // The case a hand-rolled parser gets wrong, and the reason this uses the framework's: walking
        // the header left to right answers English, and the client asked for Vietnamese.
        Assert.Equal("vi", await NegotiateAsync(acceptLanguage: "en;q=0.4,vi;q=0.9"));
    }

    [Fact]
    public async Task An_explicit_lang_beats_the_header_so_a_link_carries_its_language()
    {
        Assert.Equal("en", await NegotiateAsync(query: "?lang=en", acceptLanguage: "vi"));
    }

    [Fact]
    public async Task The_response_says_which_language_it_is_in_and_that_it_depends_on_the_header()
    {
        var context = await RunAsync(query: "", acceptLanguage: "en");

        Assert.Equal("en", context.Response.Headers.ContentLanguage);

        // Without Vary, a cache may hand one shopper's Vietnamese answer to the next asking in English.
        Assert.Contains("Accept-Language", context.Response.Headers.Vary.ToString());
    }

    [Fact]
    public void A_default_the_shop_does_not_speak_stops_startup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Localization:DefaultLanguage"] = "fr",
                ["Localization:Supported:0"] = "vi",
                ["Localization:Supported:1"] = "en",
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddRequestLanguage(configuration));

        Assert.Contains("fr", error.Message);
    }

    private static async Task<string> NegotiateAsync(string query = "", string? acceptLanguage = null) =>
        (await RunAsync(query, acceptLanguage)).Response.Headers.ContentLanguage.ToString();

    private static async Task<HttpContext> RunAsync(string query, string? acceptLanguage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Localization:DefaultLanguage"] = "vi",
                ["Localization:Supported:0"] = "vi",
                ["Localization:Supported:1"] = "en",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequestLanguage(configuration);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider.CreateScope().ServiceProvider };

        // The server does this for a real request; a hand-built pipeline has to. Without it
        // IRequestLanguage sees no request at all and answers with the default, which is exactly what
        // it should do - and is not what this test is about.
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        context.Request.Path = "/api/products";
        context.Request.QueryString = new QueryString(query);

        if (acceptLanguage is not null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        var app = new ApplicationBuilder(provider);
        app.UseRequestLanguage();
        app.Run(_ => Task.CompletedTask);

        var culture = CultureInfo.CurrentUICulture;

        try
        {
            await app.Build()(context);
        }
        finally
        {
            // The middleware sets the ambient culture for the request; put the test thread back.
            CultureInfo.CurrentUICulture = culture;
        }

        return context;
    }
}
