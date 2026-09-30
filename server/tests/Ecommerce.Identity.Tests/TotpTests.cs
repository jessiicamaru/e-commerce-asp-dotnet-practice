using System.Security.Cryptography;
using System.Text;
using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// The TOTP arithmetic (#218, specs/110), pinned to RFC 6238's own test vectors - an implementation that disagrees with
/// them disagrees with every authenticator app - and the window and replay rules around it. No database.
/// </summary>
public class TotpTests
{
    /// <summary>RFC 6238 Appendix B's SHA-1 secret: the ASCII bytes "12345678901234567890".</summary>
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void The_codes_are_RFC_6238_s_own(long unixSeconds, string eightDigits)
    {
        var step = Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);

        Assert.Equal(eightDigits, Totp.Code(RfcSecret, step, digits: 8));
        // What an app shows is the same number's last six digits.
        Assert.Equal(eightDigits[2..], Totp.Code(RfcSecret, step));
    }

    [Fact]
    public void A_code_is_accepted_one_window_either_side_and_no_further()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_010).UtcDateTime;
        var step = Totp.StepAt(now);

        Assert.Equal(step - 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 1), now, null));
        Assert.Equal(step, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step), now, null));
        Assert.Equal(step + 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step + 1), now, null));
        Assert.Null(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 2), now, null));
        Assert.Null(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step + 2), now, null));
    }

    [Fact]
    public void A_window_at_or_before_the_last_one_used_is_refused_a_code_works_once()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_010).UtcDateTime;
        var step = Totp.StepAt(now);
        var code = Totp.Code(RfcSecret, step);

        Assert.Null(Totp.Match(RfcSecret, code, now, lastUsedStep: step));
        Assert.Null(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 1), now, lastUsedStep: step - 1));
        Assert.Equal(step, Totp.Match(RfcSecret, code, now, lastUsedStep: step - 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Anything_but_six_digits_is_no_code(string? typed)
    {
        Assert.Null(Totp.Match(RfcSecret, typed, DateTime.UtcNow, null));
    }

    [Fact]
    public void Spaces_a_person_types_are_ignored()
    {
        var now = DateTime.UtcNow;
        var code = Totp.Code(RfcSecret, Totp.StepAt(now));

        Assert.NotNull(Totp.Match(RfcSecret, code[..3] + " " + code[3..], now, null));
    }

    [Fact]
    public void Base32_is_RFC_4648_and_forgiving_of_how_a_person_types_it()
    {
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Encode(RfcSecret));
        Assert.Equal(RfcSecret, Base32.Decode("gezd gnbv-gy3t qojq gezd gnbv gy3t qojq"));
        Assert.Null(Base32.Decode("NOT*BASE32"));
        var random = Totp.NewSecret();
        Assert.Equal(20, random.Length);
        Assert.Equal(random, Base32.Decode(Base32.Encode(random)));
    }

    [Fact]
    public void The_key_uri_is_what_an_authenticator_reads()
    {
        var uri = Totp.KeyUri("Ecommerce Shop", "admin@shop.vn", RfcSecret);

        Assert.Equal(
            "otpauth://totp/Ecommerce%20Shop:admin%40shop.vn?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=Ecommerce%20Shop&algorithm=SHA1&digits=6&period=30",
            uri);
    }

    [Fact]
    public void A_protected_secret_opens_only_with_its_key_and_only_untouched()
    {
        var protector = new TwoFactorSecretProtector(Options.Create(new TwoFactorOptions { Key = IdentityTestFixture.TwoFactorTestKey }));
        var sealedSecret = protector.Protect(RfcSecret);

        Assert.Equal(RfcSecret, protector.Unprotect(sealedSecret));
        Assert.NotEqual(sealedSecret, protector.Protect(RfcSecret));   // a fresh nonce every time

        var tampered = Convert.FromBase64String(sealedSecret);
        tampered[15] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(Convert.ToBase64String(tampered)));

        var otherKey = new TwoFactorSecretProtector(Options.Create(new TwoFactorOptions { Key = Convert.ToBase64String(new byte[32]) }));
        Assert.ThrowsAny<CryptographicException>(() => otherKey.Unprotect(sealedSecret));
    }

    [Theory]
    [InlineData("")]
    [InlineData("c2hvcnQ=")]
    [InlineData("not base64 at all")]
    public void A_key_that_is_not_32_bytes_is_refused_at_startup(string key)
    {
        Assert.NotEmpty(new TwoFactorOptions { Key = key }.Problems());
        Assert.Empty(new TwoFactorOptions { Key = IdentityTestFixture.TwoFactorTestKey }.Problems());
    }
}
