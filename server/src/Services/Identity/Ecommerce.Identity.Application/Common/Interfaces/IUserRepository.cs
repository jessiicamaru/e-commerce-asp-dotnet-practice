using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByUserRefreshTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes <paramref name="token"/> in favour of <paramref name="replacement"/>, and stores the
    /// replacement, in one transaction. Uses a guarded update, so two concurrent refreshes with the same
    /// token cannot both succeed: the loser gets <c>false</c> and nothing is written. Also removes this
    /// user's expired tokens, so the table does not grow forever.
    /// </summary>
    Task<bool> TryRotateRefreshTokenAsync(string token, RefreshToken replacement, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Revokes every still-active refresh token of the user: all their sessions end.</summary>
    Task<int> RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages the shop behind a seller's account (specs/027). Saved by the same
    /// <see cref="SaveChangesAsync"/> as the account and the announcement, so all three commit
    /// together or none does.
    /// </summary>
    Task AddSellerProfileAsync(SellerProfile profile, CancellationToken cancellationToken = default);

    /// <summary>The caller's own shop, or <c>null</c> when they do not sell.</summary>
    Task<SellerProfile?> GetSellerProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}