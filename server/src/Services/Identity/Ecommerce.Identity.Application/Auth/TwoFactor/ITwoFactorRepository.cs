using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Auth.TwoFactor;

/// <summary>
/// The guarded statements two-factor sign-in rests on (#218, specs/110). Each "Try" is ONE statement that decides:
/// of two requests racing with the same code, challenge or recovery code, exactly one gets true.
/// </summary>
public interface ITwoFactorRepository
{
    /// <summary>
    /// Records <paramref name="step"/> as the last window used - only when it is later than the one stored
    /// (research D5). False: that window, or a later one, was already used.
    /// </summary>
    Task<bool> TryAcceptStepAsync(Guid userId, long step, CancellationToken cancellationToken = default);

    Task AddChallengeAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default);

    /// <summary>The challenge if it may still be answered: not used, not expired, fewer than five wrong codes.</summary>
    Task<TwoFactorChallenge?> GetLiveChallengeAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Claims a live challenge once. False when it was claimed, expired or exhausted meanwhile.</summary>
    Task<bool> TryUseChallengeAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>One more wrong code against the challenge; at five it can no longer be answered.</summary>
    Task RecordChallengeFailureAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's recovery codes with these hashes - the old set stops working.</summary>
    Task ReplaceRecoveryCodesAsync(Guid userId, IReadOnlyCollection<string> codeHashes, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Spends one unused recovery code. False: no such code, or already spent.</summary>
    Task<bool> TryUseRecoveryCodeAsync(Guid userId, string codeHash, DateTime now, CancellationToken cancellationToken = default);

    Task<int> CountRecoveryCodesLeftAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Turning it off or a reset: every recovery code and challenge of the account goes.</summary>
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The session that confirmed setup becomes verified, and every other one ends (they were never verified).
    /// </summary>
    Task VerifySessionAndEndOthersAsync(Guid userId, string? keepRefreshToken, DateTime now, CancellationToken cancellationToken = default);
}
