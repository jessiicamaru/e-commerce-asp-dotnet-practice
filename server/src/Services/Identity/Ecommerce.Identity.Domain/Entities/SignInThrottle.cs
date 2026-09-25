namespace Ecommerce.Domain.Entities;

/// <summary>
/// Wrong passwords for one EMAIL, counted (specs/062). Keyed on the email, not on an account, so an address
/// with no account is counted exactly like one with an account (#28). Changed only by single atomic
/// statements, so several Identity instances agree.
/// </summary>
public class SignInThrottle
{
    /// <summary>The email as sign-in compares it (<c>EmailKey.For</c>): trimmed, lower-case.</summary>
    public string EmailKey { get; set; } = string.Empty;

    /// <summary>Wrong passwords since <see cref="WindowStartedAt"/>; back to 0 when a pause starts.</summary>
    public int Failures { get; set; }

    public DateTime WindowStartedAt { get; set; }

    /// <summary>Every sign-in for this email is answered 429 until then - the right password too.</summary>
    public DateTime? BlockedUntil { get; set; }
}
