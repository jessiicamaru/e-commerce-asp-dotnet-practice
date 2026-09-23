namespace Ecommerce.Domain.Entities;

public enum ShopApplicationStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Somebody asking to sell on the shop (specs/044). Pending until a moderator or an administrator
/// decides; only an approval makes them a seller.
/// </summary>
/// <remarks>
/// One pending application per person, enforced by a partial unique index - two tabs submitting at once
/// cannot queue the same person twice. A rejected one stays, with its reason, and they may apply again.
/// </remarks>
public class ShopApplication
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    /// <summary>What they mean to sell, in their words - what a moderator decides on.</summary>
    public string? Description { get; set; }

    public string? Phone { get; set; }

    public ShopApplicationStatus Status { get; set; } = ShopApplicationStatus.Pending;

    public string? DecisionReason { get; set; }

    public Guid? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
