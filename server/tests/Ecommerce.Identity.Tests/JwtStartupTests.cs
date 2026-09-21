using Ecommerce.Shared.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Incomplete JWT settings stop a service at startup instead of rejecting every token (issue #30).
/// </summary>
/// <remarks>
/// Calls the real <see cref="DependencyInjection.AddJwtAuthentication"/> every service calls, with
/// configuration built in memory. Lives here because Ecommerce.Shared has no test project of its own and
/// Identity is the service that signs with these settings. Needs no database.
/// </remarks>
public class JwtStartupTests
{
    private const string GoodSecret = "a-test-secret-that-is-at-least-32-bytes-long";

    private static void Start(string? secret, string? issuer, string? audience)
    {
        var settings = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = secret,
            ["JwtSettings:Issuer"] = issuer,
            ["JwtSettings:Audience"] = audience,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        new ServiceCollection().AddJwtAuthentication(configuration);
    }

    [Fact]
    public void Complete_settings_start()
    {
        Start(GoodSecret, "EcommerceApi", "EcommerceClients");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_audience_stops_startup_and_names_it(string? audience)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Start(GoodSecret, "EcommerceApi", audience));

        Assert.Contains("Audience", error.Message);
        Assert.DoesNotContain("Issuer", error.Message);
    }

    [Fact]
    public void A_missing_issuer_stops_startup_and_names_it()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Start(GoodSecret, "", "EcommerceClients"));

        Assert.Contains("Issuer", error.Message);
    }

    [Fact]
    public void A_secret_under_32_bytes_stops_startup()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Start(new string('x', 31), "EcommerceApi", "EcommerceClients"));

        Assert.Contains("32 bytes", error.Message);
    }

    [Fact]
    public void Every_problem_is_named_at_once_not_one_per_restart()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Start("short", "", ""));

        Assert.Contains("32 bytes", error.Message);
        Assert.Contains("Issuer", error.Message);
        Assert.Contains("Audience", error.Message);
    }

    [Fact]
    public void A_missing_secret_still_points_at_JWT_SECRET()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Start(null, "EcommerceApi", "EcommerceClients"));

        Assert.Contains("JWT_SECRET", error.Message);
    }
}
