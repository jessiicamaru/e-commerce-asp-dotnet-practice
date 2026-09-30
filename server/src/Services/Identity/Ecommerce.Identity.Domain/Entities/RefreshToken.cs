namespace Ecommerce.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// The session was established with a second factor (#218, specs/110). Staff roles are written into an access
    /// token only from such a session, and rotation carries it to the successor.
    /// </summary>
    public bool TwoFactorVerified { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

}