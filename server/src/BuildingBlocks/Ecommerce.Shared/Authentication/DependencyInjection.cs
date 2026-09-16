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

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
        {
            throw new InvalidOperationException(
                "JWT signing secret is not configured. Set the JWT_SECRET environment variable.");
        }

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
}
