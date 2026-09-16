namespace Ecommerce.Shared.Authentication;

/// <summary>
/// Binds the "JwtSettings" configuration section. Shared by the Identity service (which signs
/// tokens) and every resource service (which validates them), so the two sides cannot drift apart.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int ExpiryMinutes { get; init; }

    public int RefreshTokenExpiryDays { get; init; }
}
