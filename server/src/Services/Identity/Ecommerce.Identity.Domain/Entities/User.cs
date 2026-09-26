namespace Ecommerce.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the address was shown to belong to this person, by the link sent to it (specs/063). Null: not yet.
    /// Accounts from before specs/063 carry their creation time.
    /// </summary>
    public DateTime? EmailConfirmedAt { get; set; }

    public bool EmailConfirmed => EmailConfirmedAt is not null;

    /// <summary>Signed out and refused sign-in until then (specs/043). Past means not locked.</summary>
    public DateTime? LockedUntil { get; set; }

    public string? LockReason { get; set; }

    /// <summary>Refused sign-in until an administrator lifts it (specs/043). No end date.</summary>
    public DateTime? BannedAt { get; set; }

    public string? BanReason { get; set; }

    /// <summary>
    /// The language the person last used the shop in (specs/083) - from their registration, sign-in or session
    /// renewal. What an email about them is written in when its sender cannot know. Null: never said.
    /// </summary>
    public string? Language { get; set; }

    public bool IsLocked(DateTime now) => LockedUntil is { } until && until > now;

    public bool IsBanned => BannedAt is not null;

    public ICollection<Role> Roles { get; set; } = new List<Role>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}