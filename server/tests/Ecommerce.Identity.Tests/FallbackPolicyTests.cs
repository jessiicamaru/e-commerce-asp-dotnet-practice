using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// An endpoint that says nothing is signed-in only (#183, specs/089): the fallback policy every service gets from
/// <see cref="DependencyInjection.AddJwtAuthentication"/>, through a real ASP.NET Core pipeline.
/// </summary>
/// <remarks>
/// Lives here for the reason <see cref="JwtStartupTests"/> does: Ecommerce.Shared has no test project of its own.
/// Needs no database.
/// </remarks>
public class FallbackPolicyTests
{
    private const string Secret = "a-test-secret-that-is-at-least-32-bytes-long";
    private const string Issuer = "EcommerceApi";
    private const string Audience = "EcommerceClients";

    [Fact]
    public async Task An_endpoint_that_says_nothing_refuses_an_anonymous_caller()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/says-nothing");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_endpoint_that_says_it_is_public_is_public()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_caller_reaches_an_endpoint_that_says_nothing()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        var response = await client.GetAsync("/says-nothing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = Secret,
            ["JwtSettings:Issuer"] = Issuer,
            ["JwtSettings:Audience"] = Audience,
        });
        builder.Services.AddJwtAuthentication(builder.Configuration);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/says-nothing", () => "reached");
        app.MapGet("/public", () => "reached").AllowAnonymous();
        await app.StartAsync();
        return app;
    }

    private static string Token() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Audience,
        Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString())]),
        IssuedAt = DateTime.UtcNow,
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
    });
}
