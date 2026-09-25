namespace Ecommerce.Domain.Entities;

/// <summary>
/// A link sent to confirm that an address belongs to its account (specs/063). Only its SHA-256 hash is kept,
/// like a reset link (specs/061). Valid for 24 hours, once.
/// </summary>
public class EmailConfirmationToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hex SHA-256 of the token in the link. Unique - it is how a confirmation finds its row.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set by the one guarded statement that uses it; a second use finds it set.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
