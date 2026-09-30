namespace Ecommerce.Domain.Entities;

/// <summary>
/// A one-time code for a lost phone (#218, specs/110): ten per account, shown once, kept only as a SHA-256 hash - like
/// a password-reset token, a database read never yields a usable one. Spent by one guarded statement.
/// </summary>
public class TwoFactorRecoveryCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hex SHA-256 of the code as normalised (upper case, no dash).</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }
}

/// <summary>
/// Between the right password and the code (#218, specs/110 research D3): it grants nothing by itself, lives five
/// minutes, dies after five wrong codes, and is claimed once. Only its hash is kept.
/// </summary>
public class TwoFactorChallenge
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? UsedAt { get; set; }
}
