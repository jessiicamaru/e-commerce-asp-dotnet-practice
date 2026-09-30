using System.Security.Cryptography;
using Ecommerce.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure.Security;

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

/// <summary>
/// AES-GCM under <see cref="TwoFactorOptions.Key"/>: <c>base64(nonce ‖ ciphertext ‖ tag)</c> (research D4). GCM
/// authenticates, so a row changed by hand fails to decrypt instead of producing somebody else's codes.
/// </summary>
public class TwoFactorSecretProtector(IOptions<TwoFactorOptions> options) : ITwoFactorSecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = Convert.FromBase64String(options.Value.Key);

    public string Protect(byte[] secret)
    {
        var output = new byte[NonceSize + secret.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, secret, output.AsSpan(NonceSize, secret.Length), output.AsSpan(NonceSize + secret.Length, TagSize));
        return Convert.ToBase64String(output);
    }

    public byte[] Unprotect(string protectedSecret)
    {
        var input = Convert.FromBase64String(protectedSecret);
        if (input.Length <= NonceSize + TagSize)
            throw new CryptographicException("Not a protected two-factor secret.");

        var length = input.Length - NonceSize - TagSize;
        var secret = new byte[length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(input.AsSpan(0, NonceSize), input.AsSpan(NonceSize, length), input.AsSpan(NonceSize + length, TagSize), secret);
        return secret;
    }
}
