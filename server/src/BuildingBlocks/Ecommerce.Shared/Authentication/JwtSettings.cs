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

    /// <summary>
    /// Whom a back-office session's tokens are issued for (#280, specs/139, ADR-003). Every service accepts it beside
    /// <see cref="Audience"/>, and honours Admin or Moderator only in a token issued for it. A default, so no service's
    /// configuration has to name it - a string is safe from the binder's array-append trap. ⚠️ Present but empty (an
    /// environment variable set to nothing) is the default too: an empty audience would refuse every back-office token in
    /// every service, which a test found the binder doing with a null value.
    /// </summary>
    public string BackOfficeAudience
    {
        get => string.IsNullOrWhiteSpace(_backOfficeAudience) ? DefaultBackOfficeAudience : _backOfficeAudience;
        init => _backOfficeAudience = value;
    }

    private readonly string? _backOfficeAudience;

    public const string DefaultBackOfficeAudience = "EcommerceBackOffice";

    public int ExpiryMinutes { get; init; }

    public int RefreshTokenExpiryDays { get; init; }
}
