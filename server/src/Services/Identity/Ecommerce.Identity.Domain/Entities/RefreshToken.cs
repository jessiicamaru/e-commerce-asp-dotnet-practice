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

    /// <summary>
    /// The application this session was made for (#278, specs/138), from the sign-in's <c>Origin</c> and carried across
    /// every rotation. Null for every session from before it - read as <see cref="SessionClient.Storefront"/>, the less
    /// privileged answer.
    /// </summary>
    public SessionClient? Client { get; set; }

    public SessionClient App => Client ?? SessionClient.Storefront;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

}