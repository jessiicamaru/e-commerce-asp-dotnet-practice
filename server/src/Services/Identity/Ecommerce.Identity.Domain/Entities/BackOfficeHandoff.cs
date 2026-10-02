namespace Ecommerce.Domain.Entities;

/// <summary>
/// A single-use code a signed-in staff member carries from the storefront to the back office (#279, specs/140). It
/// stands in for the password, never for the second factor: redeemed, it yields a two-factor challenge. Only its
/// SHA-256 is kept, for 30 seconds, used once.
/// </summary>
public class BackOfficeHandoff
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hex SHA-256 of the code. Unique - it is how a redemption finds its row.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set by the one guarded statement that redeems it; a second redemption finds it set.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
