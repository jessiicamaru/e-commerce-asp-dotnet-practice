using System.Security.Cryptography;
using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure.Security;

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
