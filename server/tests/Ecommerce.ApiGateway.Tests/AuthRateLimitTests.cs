using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.ApiGateway.Tests;

/// <summary>
/// The anonymous auth endpoints, limited per client IP at the gateway (specs/062, #105) - through the real
/// pipeline: forwarded headers, routing, the limiter, YARP. Every destination is unreachable, so a request
/// the limiter lets through gets 502 from the proxy and one it refuses gets 429 without reaching anything.
/// </summary>
public class AuthRateLimitTests
{
    private const string Unreachable = "http://127.0.0.1:9/";

    [Fact]
    public async Task Asking_for_reset_links_too_fast_is_429_with_how_long_to_wait()
    {
        using var gateway = Gateway(("RateLimits:email:PermitLimit", "2"));
        var client = gateway.CreateClient();

        Assert.Equal(HttpStatusCode.BadGateway, (await ForgotAsync(client, "10.0.0.1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, (await ForgotAsync(client, "10.0.0.1")).StatusCode);
        var refused = await ForgotAsync(client, "10.0.0.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        var seconds = int.Parse(refused.Headers.GetValues("Retry-After").Single());
        Assert.InRange(seconds, 1, 60);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Equal(seconds, body.GetProperty("retryAfter").GetInt32());
    }

    [Fact]
    public async Task Each_client_has_its_own_allowance()
    {
        using var gateway = Gateway(("RateLimits:email:PermitLimit", "1"));
        var client = gateway.CreateClient();

        await ForgotAsync(client, "10.0.0.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ForgotAsync(client, "10.0.0.1")).StatusCode);

        Assert.Equal(HttpStatusCode.BadGateway, (await ForgotAsync(client, "10.0.0.2")).StatusCode);
    }

    /// <summary>Asking for reset links too fast does not stop the same client signing in.</summary>
    [Fact]
    public async Task Using_up_one_limit_leaves_the_others()
    {
        using var gateway = Gateway(("RateLimits:email:PermitLimit", "1"));
        var client = gateway.CreateClient();

        await ForgotAsync(client, "10.0.0.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ForgotAsync(client, "10.0.0.1")).StatusCode);

        Assert.Equal(HttpStatusCode.BadGateway, (await PostAsync(client, "/api/auth/login", "10.0.0.1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, (await PostAsync(client, "/api/auth/refresh", "10.0.0.1")).StatusCode);
    }

    [Fact]
    public async Task Sign_in_registration_and_reset_share_one_allowance()
    {
        using var gateway = Gateway(("RateLimits:sign-in:PermitLimit", "4"));
        var client = gateway.CreateClient();

        foreach (var path in new[] { "/api/auth/login", "/api/auth/register", "/api/auth/register-seller", "/api/auth/reset-password" })
        {
            Assert.Equal(HttpStatusCode.BadGateway, (await PostAsync(client, path, "10.0.0.1")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(client, "/api/auth/login", "10.0.0.1")).StatusCode);
    }

    [Fact]
    public async Task Refreshing_has_its_own_allowance()
    {
        using var gateway = Gateway(("RateLimits:session:PermitLimit", "2"));
        var client = gateway.CreateClient();

        await PostAsync(client, "/api/auth/refresh", "10.0.0.1");
        await PostAsync(client, "/api/auth/refresh", "10.0.0.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(client, "/api/auth/refresh", "10.0.0.1")).StatusCode);
    }

    [Fact]
    public async Task Nothing_else_is_limited()
    {
        using var gateway = Gateway(("RateLimits:sign-in:PermitLimit", "1"), ("RateLimits:session:PermitLimit", "1"));
        var client = gateway.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadGateway, (await PostAsync(client, "/api/auth/logout", "10.0.0.1")).StatusCode);
            Assert.Equal(HttpStatusCode.BadGateway, (await SendAsync(client, HttpMethod.Get, "/api/products", "10.0.0.1")).StatusCode);
        }
    }

    /// <summary>Anybody can write X-Forwarded-For: from a peer nobody trusts, a new one each time changes nothing.</summary>
    [Fact]
    public async Task A_forwarded_address_from_an_untrusted_peer_is_ignored()
    {
        using var gateway = Gateway(("RateLimits:email:PermitLimit", "1"));
        var client = gateway.CreateClient();

        await ForgotAsync(client, "10.0.0.1", forwardedFor: "203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ForgotAsync(client, "10.0.0.1", forwardedFor: "203.0.113.2")).StatusCode);
    }

    /// <summary>Behind the storefront's nginx every browser shares nginx's address - unless nginx is trusted.</summary>
    [Fact]
    public async Task A_trusted_proxy_forwards_each_client_address()
    {
        using var gateway = Gateway(("RateLimits:email:PermitLimit", "1"), ("GATEWAY_TRUSTED_PROXIES", "172.30.10.10"));
        var client = gateway.CreateClient();

        await ForgotAsync(client, "172.30.10.10", forwardedFor: "203.0.113.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ForgotAsync(client, "172.30.10.10", forwardedFor: "203.0.113.1")).StatusCode);

        Assert.Equal(HttpStatusCode.BadGateway, (await ForgotAsync(client, "172.30.10.10", forwardedFor: "203.0.113.2")).StatusCode);
    }

    /// <summary>A client cannot hide behind a trusted proxy by writing its own entries further left.</summary>
    [Fact]
    public async Task Only_the_hop_the_trusted_proxy_added_counts()
    {
        using var gateway = Gateway(("RateLimits:email:PermitLimit", "1"), ("GATEWAY_TRUSTED_PROXIES", "172.30.10.0/24"));
        var client = gateway.CreateClient();

        await ForgotAsync(client, "172.30.10.10", forwardedFor: "198.51.100.7, 203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await ForgotAsync(client, "172.30.10.10", forwardedFor: "198.51.100.8, 203.0.113.1")).StatusCode);
    }

    [Theory]
    [InlineData("RateLimits:sign-in:PermitLimit", "0")]
    [InlineData("RateLimits:email:WindowSeconds", "-1")]
    [InlineData("GATEWAY_TRUSTED_PROXIES", "the-storefront")]
    public void A_setting_that_would_switch_a_limit_off_refuses_to_start(string key, string value)
    {
        using var gateway = Gateway((key, value));

        var refused = Assert.ThrowsAny<Exception>(() => gateway.CreateClient());
        Assert.Contains(key.Split(':')[0], refused.ToString());
    }

    // ------------------------------------------------------------------ helpers

    private static WebApplicationFactory<Program> Gateway(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var cluster in new[] { "identity", "catalog", "order", "inventory", "payment", "cart", "activity" })
            {
                builder.UseSetting($"ReverseProxy:Clusters:{cluster}-cluster:Destinations:destination1:Address", Unreachable);
            }

            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestPeer>());
        });

    private static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string peer, string? forwardedFor = null) =>
        PostAsync(client, "/api/auth/forgot-password", peer, forwardedFor);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string peer, string? forwardedFor = null) =>
        SendAsync(client, HttpMethod.Post, path, peer, forwardedFor);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string peer, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestPeer.Header, peer);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        if (method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(new { email = "lan@demo.test" });
        }

        return client.SendAsync(request);
    }

    /// <summary>
    /// The test server has no network peer, so this sets the connection's address from a test header - before
    /// anything of the gateway's own runs, exactly where a real connection's address would already be.
    /// </summary>
    private sealed class TestPeer : IStartupFilter
    {
        public const string Header = "X-Test-Peer";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[Header], out var peer))
                {
                    context.Connection.RemoteIpAddress = peer;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
