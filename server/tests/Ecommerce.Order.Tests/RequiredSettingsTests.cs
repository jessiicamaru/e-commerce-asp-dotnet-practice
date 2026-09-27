using Ecommerce.Order.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Order refuses to start without a setting it cannot work without (#210, specs/103) - the commission rate included,
/// which it used to discover at the first checkout, as a 500. Runs the check over the real registration; no database.
/// </summary>
public class RequiredSettingsTests
{
    private static readonly Dictionary<string, string?> Complete = new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=never_opened",
        ["Shipping:Options:0:Code"] = "standard",
        ["Shipping:Options:0:Name"] = "Standard delivery",
        ["Shipping:Options:0:Prices:VND"] = "30000",
        ["Money:DefaultCurrency"] = "VND",
        ["Money:Supported:0:Code"] = "VND",
        ["Money:Supported:0:Decimals"] = "0",
        ["Tax:DefaultRate"] = "0.10",
        ["Marketplace:CommissionRate"] = "0.1",
    };

    [Fact]
    public void A_complete_configuration_starts() => Check(Complete);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    [InlineData("ten percent")]
    public void A_missing_or_impossible_commission_rate_stops_it(string? rate)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Check(With("Marketplace:CommissionRate", rate)));
        Assert.Contains("Marketplace:CommissionRate", refused.Message);
    }

    [Fact]
    public void Missing_shipping_options_still_stop_it() =>
        Assert.ThrowsAny<Exception>(() => Check(With("Shipping:Options:0:Code", null, "Shipping:Options:0:Name", "Shipping:Options:0:Prices:VND")));

    // ------------------------------------------------------------------ helpers

    private static Dictionary<string, string?> With(string key, string? value, params string[] removed)
    {
        var settings = new Dictionary<string, string?>(Complete);
        if (value is null) settings.Remove(key); else settings[key] = value;
        foreach (var other in removed) settings.Remove(other);
        return settings;
    }

    private static void Check(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        RequiredSettings.Check(provider);
    }
}
