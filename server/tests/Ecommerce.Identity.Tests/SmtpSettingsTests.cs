using Ecommerce.Infrastructure.Email;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Email that a real provider accepts (specs/141): an account and STARTTLS when configured, plain SMTP to Mailpit when not,
/// and a half-configured account refused at startup rather than at the first email.
/// </summary>
public class SmtpSettingsTests
{
    private static SmtpOptions From(Dictionary<string, string?> environment)
    {
        var options = new SmtpOptions();
        SmtpSettings.Apply(options, name => environment.GetValueOrDefault(name));
        return options;
    }

    [Fact]
    public void Nothing_configured_is_plain_smtp_to_mailpit()
    {
        var options = From([]);

        using var client = SmtpEmailTransport.CreateClient(options);

        Assert.Equal(("localhost", 1025, false), (client.Host, client.Port, client.EnableSsl));
        Assert.Null(client.Credentials);
        Assert.Empty(options.Problems());
    }

    [Fact]
    public void A_provider_is_signed_in_to_over_starttls()
    {
        var options = From(new()
        {
            ["SMTP_HOST"] = "smtp.example.com", ["SMTP_PORT"] = "587", ["SMTP_USERNAME"] = "shop",
            ["SMTP_PASSWORD"] = "app-password", ["SMTP_TLS"] = "true", ["SMTP_FROM"] = "Shop <shop@example.com>",
        });

        using var client = SmtpEmailTransport.CreateClient(options);

        Assert.Equal(("smtp.example.com", 587, true), (client.Host, client.Port, client.EnableSsl));
        var credential = Assert.IsType<System.Net.NetworkCredential>(client.Credentials);
        Assert.Equal(("shop", "app-password"), (credential.UserName, credential.Password));
        Assert.Equal("Shop <shop@example.com>", options.From);
        Assert.Empty(options.Problems());
    }

    [Theory]
    [InlineData("shop", null)]
    [InlineData(null, "app-password")]
    public void Half_an_account_is_refused(string? username, string? password)
    {
        var options = From(new() { ["SMTP_USERNAME"] = username, ["SMTP_PASSWORD"] = password });

        Assert.Contains(options.Problems(), p => p.Contains("SMTP_USERNAME and SMTP_PASSWORD"));
    }

    [Fact]
    public void A_sender_that_is_not_an_address_is_refused()
    {
        Assert.Contains(From(new() { ["SMTP_FROM"] = "not an address" }).Problems(), p => p.Contains("SMTP_FROM"));
    }
}
