namespace Ecommerce.Application.Auth.TwoFactor;

/// <summary>The settings of two-factor sign-in (#218, specs/110).</summary>
public class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    /// <summary>
    /// 32 bytes, base64 - the AES-256 key the TOTP secrets are encrypted with. <c>TWO_FACTOR_KEY</c> overrides it. There
    /// is no default: Identity refuses to start without a usable key, as it does for the JWT secret, rather than store
    /// secrets under a key everybody who has read the repository knows.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The name an authenticator app shows above the code.</summary>
    public string Issuer { get; set; } = "EcommerceShop";

    public IEnumerable<string> Problems()
    {
        byte[]? key = null;
        try { key = Convert.FromBase64String(Key ?? string.Empty); } catch (FormatException) { }
        if (key is not { Length: 32 })
            yield return "TwoFactor:Key (TWO_FACTOR_KEY) must be 32 random bytes in base64.";
        if (string.IsNullOrWhiteSpace(Issuer))
            yield return "TwoFactor:Issuer must not be empty.";
    }
}
