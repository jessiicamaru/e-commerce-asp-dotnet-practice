using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class TwoFactorRepository(ApplicationDbContext context) : ITwoFactorRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<bool> TryAcceptStepAsync(Guid userId, long step, CancellationToken cancellationToken = default) =>
        await _context.Users
            .Where(u => u.Id == userId && (u.TwoFactorLastStep == null || u.TwoFactorLastStep < step))
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.TwoFactorLastStep, step), cancellationToken) == 1;

    public async Task AddChallengeAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default) =>
        await _context.TwoFactorChallenges.AddAsync(challenge, cancellationToken);

    public Task<TwoFactorChallenge?> GetLiveChallengeAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default) =>
        _context.TwoFactorChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.TokenHash == tokenHash && c.UsedAt == null
            && c.ExpiresAt > now && c.FailedAttempts < TwoFactorChallenge.MaxFailedAttempts, cancellationToken);

    public async Task<bool> TryUseChallengeAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default) =>
        await _context.TwoFactorChallenges
            .Where(c => c.TokenHash == tokenHash && c.UsedAt == null && c.ExpiresAt > now
                && c.FailedAttempts < TwoFactorChallenge.MaxFailedAttempts)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.UsedAt, now), cancellationToken) == 1;

    public Task RecordChallengeFailureAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        // An increment in SQL: two wrong codes at once count as two, never as one.
        _context.TwoFactorChallenges.Where(c => c.TokenHash == tokenHash)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts + 1), cancellationToken);

    public async Task ReplaceRecoveryCodesAsync(
        Guid userId, IReadOnlyCollection<string> codeHashes, DateTime now, CancellationToken cancellationToken = default)
    {
        await _context.TwoFactorRecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.TwoFactorRecoveryCodes.AddRangeAsync(codeHashes.Select(hash => new TwoFactorRecoveryCode
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CodeHash = hash,
            CreatedAt = now,
        }), cancellationToken);
    }

    public async Task<bool> TryUseRecoveryCodeAsync(Guid userId, string codeHash, DateTime now, CancellationToken cancellationToken = default) =>
        await _context.TwoFactorRecoveryCodes
            .Where(c => c.UserId == userId && c.CodeHash == codeHash && c.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.UsedAt, now), cancellationToken) == 1;

    public Task<int> CountRecoveryCodesLeftAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.TwoFactorRecoveryCodes.CountAsync(c => c.UserId == userId && c.UsedAt == null, cancellationToken);

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.TwoFactorRecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.TwoFactorChallenges.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task VerifySessionAndEndOthersAsync(Guid userId, string? keepRefreshToken, DateTime now, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.Token != keepRefreshToken)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), cancellationToken);

        if (keepRefreshToken is not null)
        {
            await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.Token == keepRefreshToken && t.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.TwoFactorVerified, true), cancellationToken);
        }
    }
}
