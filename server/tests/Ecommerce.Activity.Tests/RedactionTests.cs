using Ecommerce.Shared.Audit;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// ⚠️ FR-005: a secret must never leave the service that holds it. Redaction happens in the PUBLISHER's
/// snapshot, before the message exists - a log that stored it and hid it on screen would still hold it.
/// </summary>
public class RedactionTests
{
    [Fact]
    public void Secret_looking_fields_are_redacted_at_any_depth()
    {
        var json = AuditSnapshot.Serialize(new
        {
            Email = "lan@demo.test",
            PasswordHash = "AQAAAAEAACcQ",
            Nested = new { RefreshToken = "abc", Name = "Lan" },
            Keys = new[] { new { ClientSecret = "s" } }
        })!;

        Assert.DoesNotContain("AQAAAAEAACcQ", json);
        Assert.DoesNotContain("\"abc\"", json);
        Assert.DoesNotContain("\"s\"", json);
        Assert.Contains("lan@demo.test", json);
        Assert.Contains("\"Lan\"", json);
        Assert.Contains(AuditSnapshot.Redacted, json);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("NewPassword", true)]
    [InlineData("accessToken", true)]
    [InlineData("jwtSecret", true)]
    [InlineData("passwordHash", true)]
    [InlineData("email", false)]
    [InlineData("name", false)]
    public void Names_decide_what_is_secret(string name, bool secret)
    {
        Assert.Equal(secret, AuditSnapshot.IsSecret(name));
    }

    [Fact]
    public void Nothing_is_nothing()
    {
        Assert.Null(AuditSnapshot.Serialize(null));
    }
}
