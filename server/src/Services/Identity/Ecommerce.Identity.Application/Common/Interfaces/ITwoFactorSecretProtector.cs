namespace Ecommerce.Application.Common.Interfaces;

/// <summary>
/// Encrypts a TOTP secret at rest (#218, specs/110 research D4). A secret cannot be hashed like a password - codes are
/// computed from it - so it is encrypted, and a row that was tampered with fails to open rather than yielding codes.
/// </summary>
public interface ITwoFactorSecretProtector
{
    string Protect(byte[] secret);

    /// <exception cref="System.Security.Cryptography.CryptographicException">The text was not made with this key.</exception>
    byte[] Unprotect(string protectedSecret);
}
