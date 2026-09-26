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

    /// <summary>
    /// Remembers the language a person is using the shop in (specs/083): one guarded statement that writes nothing
    /// when it is already the one on record - a session renewal calls it every few minutes.
    /// </summary>
    Task RecordLanguageAsync(Guid userId, string language, CancellationToken cancellationToken = default);

    /// <summary>Revokes every still-active refresh token of the user: all their sessions end.</summary>
    Task<int> RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every still-active refresh token of the user except <paramref name="keep"/> (specs/064): the
    /// session that made a change stays, every other ends. A null <paramref name="keep"/> ends them all.
    /// </summary>
    Task<int> RevokeOtherRefreshTokensAsync(Guid userId, string? keep, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>One user with their roles, tracked - for staff acting on them (specs/043).</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Users whose email or name contains <paramref name="search"/> (case- and accent-blind on email via
    /// its lower-case form), newest first, with their roles (specs/043).
    /// </summary>
    Task<(List<User> Items, int TotalCount)> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Who these ids are, for an administrator's report (specs/047). Unknown ids are left out.</summary>
    Task<List<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>How many accounts hold each role, and how many are stopped (specs/047).</summary>
    Task<(Dictionary<string, int> ByRole, int Locked, int Banned, int Total)> CountAsync(DateTime now, CancellationToken cancellationToken = default);

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