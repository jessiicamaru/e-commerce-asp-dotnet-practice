using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A token not issued for the back office never acts as staff (#280, specs/139): every service's
/// <see cref="DependencyInjection.AddJwtAuthentication"/> accepts the storefront's audience and the back office's, and
/// keeps Admin or Moderator only in a token issued for the back office - through a real ASP.NET Core pipeline.
/// </summary>
/// <remarks>Lives here for the reason <see cref="FallbackPolicyTests"/> does: Ecommerce.Shared has no test project.</remarks>
public class BackOfficeAudienceTests
{
    private const string Secret = "a-test-secret-that-is-at-least-32-bytes-long";
    private const string Issuer = "EcommerceApi";
    private const string Storefront = "EcommerceClients";

    [Fact]
    public async Task A_back_office_token_acts_as_staff()
    {
        await using var app = await StartAsync();

        var response = await Client(app, Token(JwtSettings.DefaultBackOfficeAudience, "Admin")).GetAsync("/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The second line behind Identity (specs/138): roles written into a storefront token are not honoured.</summary>
    [Fact]
    public async Task A_storefront_token_carrying_admin_is_refused_staff_and_keeps_everything_else()
    {
        await using var app = await StartAsync();
        var client = Client(app, Token(Storefront, "Admin", "Customer", "Moderator"));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/signed-in")).StatusCode);
        Assert.Equal("Customer", await client.GetStringAsync("/roles"));
    }

    [Fact]
    public async Task A_token_for_neither_app_is_refused_altogether()
    {
        await using var app = await StartAsync();

        var response = await Client(app, Token("SomebodyElse", "Admin")).GetAsync("/signed-in");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_configured_back_office_audience_is_the_one_honoured()
    {
        await using var app = await StartAsync(backOfficeAudience: "PortalOnly");

        Assert.Equal(HttpStatusCode.OK, (await Client(app, Token("PortalOnly", "Moderator")).GetAsync("/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client(app, Token(JwtSettings.DefaultBackOfficeAudience, "Moderator")).GetAsync("/staff")).StatusCode);
    }

    /// <summary>An environment variable set to nothing is the default, not an audience nobody's token has.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_back_office_audience_is_the_default(string configured)
    {
        await using var app = await StartAsync(backOfficeAudience: configured);

        var response = await Client(app, Token(JwtSettings.DefaultBackOfficeAudience, "Admin")).GetAsync("/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpClient Client(WebApplication app, string token)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<WebApplication> StartAsync(string? backOfficeAudience = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = Secret,
            ["JwtSettings:Issuer"] = Issuer,
            ["JwtSettings:Audience"] = Storefront,
            ["JwtSettings:BackOfficeAudience"] = backOfficeAudience,
        });
        builder.Services.AddJwtAuthentication(builder.Configuration);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/staff", () => "staff").RequireAuthorization(new AuthorizeAttribute { Roles = StaffRoles.Staff });
        app.MapGet("/signed-in", () => "reached");
        app.MapGet("/roles", (HttpContext http) => string.Join(",", http.User.FindAll("role").Select(c => c.Value)));
        await app.StartAsync();
        return app;
    }

    private static string Token(string audience, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = Issuer,
            Audience = audience,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
        });
    }
}
