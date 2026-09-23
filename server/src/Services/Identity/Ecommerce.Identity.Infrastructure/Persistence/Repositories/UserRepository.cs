using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class UserRepository(ApplicationDbContext _context) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Case-insensitive (#49): lower("Email") = @key, served by IX_users_Email_lower.
        var key = EmailKey.For(email);

        return await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == key, cancellationToken);
    }

    public async Task<User?> GetByUserRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == token), cancellationToken);
    }

    public async Task<bool> TryRotateRefreshTokenAsync(
        string token, RefreshToken replacement, DateTime now, CancellationToken cancellationToken = default)
    {
        // EnableRetryOnFailure is on for this context, and a retrying strategy refuses a user-initiated
        // transaction unless the whole unit runs inside it (the same reason UnitOfWork does this).
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // One statement decides who wins. Reading "RevokedAt is null" and then writing would let two
            // requests both see an active token and both mint a successor.
            var revoked = await _context.RefreshTokens
                .Where(t => t.Token == token && t.RevokedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.ReplacedByToken, replacement.Token), cancellationToken);

            if (revoked == 0)
            {
                return false;   // the transaction rolls back on dispose; nothing was written
            }

            // Expired tokens can no longer be replayed, so they are no longer evidence of anything.
            // Revoked-but-unexpired ones stay: they are what recognises a stolen token coming back.
            await _context.RefreshTokens
                .Where(t => t.UserId == replacement.UserId && t.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);

            if (_context.Entry(replacement).State == EntityState.Detached)
            {
                _context.RefreshTokens.Add(replacement);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public Task<int> RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<(List<User> Items, int TotalCount)> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(key)
                || (u.FirstName + " " + u.LastName).ToLower().Contains(key));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(u => u.Roles)
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task AddSellerProfileAsync(SellerProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.SellerProfiles.AddAsync(profile, cancellationToken);
    }

    public Task<SellerProfile?> GetSellerProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.SellerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
