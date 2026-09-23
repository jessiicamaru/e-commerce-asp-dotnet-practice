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

    /// <summary>Signed out and refused sign-in until then (specs/043). Past means not locked.</summary>
    public DateTime? LockedUntil { get; set; }

    public string? LockReason { get; set; }

    /// <summary>Refused sign-in until an administrator lifts it (specs/043). No end date.</summary>
    public DateTime? BannedAt { get; set; }

    public string? BanReason { get; set; }

    public bool IsLocked(DateTime now) => LockedUntil is { } until && until > now;

    public bool IsBanned => BannedAt is not null;

    public ICollection<Role> Roles { get; set; } = new List<Role>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}