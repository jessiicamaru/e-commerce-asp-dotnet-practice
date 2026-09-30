using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Ecommerce.Application.Auth.TwoFactor;

/// <summary>
/// Time-based one-time passwords, RFC 6238 (#218, specs/110): HMAC-SHA1 over the 30-second window, six digits - the
/// parameters every authenticator app assumes. How and why it works: docs/features/auth/totp-two-factor.md.
/// </summary>
/// <remarks>
/// Pure: no clock of its own and no storage. The caller passes the time and the last step it accepted, and persists the
/// step <see cref="Match"/> returns with a guarded write - that write, not this class, is what makes a code single-use
/// when two sign-ins race (research D5).
/// </remarks>
public static class Totp
{
    public const int Digits = 6;
    public const int PeriodSeconds = 30;

    /// <summary>One window either side of now: a slow typist and a phone a few seconds off still pass (RFC 6238 §5.2).</summary>
    public const int Drift = 1;

    /// <summary>A new secret: 20 random bytes, the size of an HMAC-SHA1 key (RFC 4226 §4 recommends 160 bits).</summary>
    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    /// <summary>The number of 30-second windows since 1970 - what changes, and so what changes the code.</summary>
    public static long StepAt(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds() / PeriodSeconds;

    /// <summary>The code for one window: HMAC-SHA1(secret, step), dynamically truncated (RFC 4226 §5.3).</summary>
    public static string Code(byte[] secret, long step, int digits = Digits)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);

        // The low nibble of the last byte picks where to read four bytes; the top bit is cleared so the number is the
        // same read as signed or unsigned.
        var offset = hash[^1] & 0x0F;
        var number = BinaryPrimitives.ReadInt32BigEndian(hash.Slice(offset, 4)) & 0x7FFFFFFF;
        return (number % (int)Math.Pow(10, digits)).ToString().PadLeft(digits, '0');
    }

    /// <summary>
    /// The window a typed code belongs to, or null: checked for the window before, now and after, never one at or
    /// before <paramref name="lastUsedStep"/> - a code works once. Compared in constant time.
    /// </summary>
    public static long? Match(byte[] secret, string? typed, DateTime now, long? lastUsedStep)
    {
        var code = (typed ?? string.Empty).Replace(" ", string.Empty);
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
            return null;

        var current = StepAt(now);
        long? matched = null;
        // Every window is computed and compared, even after a match, so how long this takes says nothing.
        for (var step = current - Drift; step <= current + Drift; step++)
        {
            var same = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Code(secret, step)), Encoding.ASCII.GetBytes(code));
            if (same && (lastUsedStep is null || step > lastUsedStep) && matched is null)
                matched = step;
        }

        return matched;
    }

    /// <summary>
    /// What the QR code holds (the Key URI format every authenticator reads): the issuer and account label the app
    /// shows, and the secret in base32. The defaults (SHA1, 6, 30) are spelled out anyway.
    /// </summary>
    public static string KeyUri(string issuer, string account, byte[] secret)
    {
        var label = Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(account);
        return $"otpauth://totp/{label}?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}"
            + $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
    }
}

/// <summary>RFC 4648 base32 (A-Z, 2-7), no padding - how a TOTP secret is written for people and apps.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    /// <summary>Spaces, dashes, padding and case are ignored, as a person may type them. Null when not base32.</summary>
    public static byte[]? Decode(string? text)
    {
        var clean = (text ?? string.Empty).Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        if (clean.Length == 0)
            return null;

        var output = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0)
                return null;
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
