using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Shared.Authentication;

public static class DependencyInjection
{
    /// <summary>
    /// Registers JWT bearer validation using the same issuer, audience and signing key that the
    /// Identity service signs with. Call this in every service that exposes protected endpoints,
    /// then add app.UseAuthentication() before app.UseAuthorization().
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{JwtSettings.SectionName}' is missing.");

        EnsureComplete(jwtSettings);

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Without this, the handler rewrites "sub" to ClaimTypes.NameIdentifier and
                // every lookup by "sub" silently returns null.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Secret)),

                    ValidateLifetime = true,
                    // Default skew is 5 minutes, which would keep a 15-minute token alive for 20.
                    ClockSkew = TimeSpan.Zero,

                    NameClaimType = JwtRegisteredClaimNames.Sub,

                    // The signing side adds roles as ClaimTypes.Role, but JwtSecurityTokenHandler
                    // shortens that to "role" on the way out. With MapInboundClaims disabled the
                    // name is not expanded again, so "role" is what actually arrives.
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits; a shorter one fails on every signing.</summary>
    public const int MinimumSecretBytes = 32;

    /// <summary>
    /// Refuses to start with settings that would reject every token (issue #30).
    /// </summary>
    /// <remarks>
    /// An empty <c>Issuer</c> or <c>Audience</c> used to start cleanly, report healthy, and answer every
    /// request with 401 <c>IDX10208</c> - which reads as a permissions bug, not a configuration one. It
    /// happened to Cart, which had no <c>appsettings.json</c>. The constitution says a missing required
    /// setting fails at startup, so every problem is named here at once, rather than one per restart.
    /// </remarks>
    public static void EnsureComplete(JwtSettings settings)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.Secret))
        {
            problems.Add("the signing secret is not configured - set the JWT_SECRET environment variable");
        }
        else if (Encoding.UTF8.GetByteCount(settings.Secret) < MinimumSecretBytes)
        {
            problems.Add($"the signing secret is shorter than {MinimumSecretBytes} bytes, too short for HMAC-SHA256 - set a longer JWT_SECRET");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            problems.Add($"'{JwtSettings.SectionName}:Issuer' is empty - add it to this service's appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            problems.Add($"'{JwtSettings.SectionName}:Audience' is empty - add it to this service's appsettings.json");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"JWT settings are incomplete, so every token would be rejected: {string.Join("; ", problems)}.");
        }
    }
}
