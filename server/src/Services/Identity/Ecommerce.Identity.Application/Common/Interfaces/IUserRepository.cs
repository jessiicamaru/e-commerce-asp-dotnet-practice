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

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}