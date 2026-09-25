namespace Ecommerce.Domain.Entities;

/// <summary>
/// A link somebody asked for to choose a new password (specs/061). Only its SHA-256 hash is kept: a person
/// reading the database cannot use one. Valid for 30 minutes, once.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hex SHA-256 of the token in the link. Unique - it is how a reset finds its row.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set by the one guarded statement that uses it; a second use finds it set.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
