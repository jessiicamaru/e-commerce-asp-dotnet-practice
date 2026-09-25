using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class PasswordResetRepository(ApplicationDbContext context) : IPasswordResetRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
        await _context.PasswordResetTokens.AddAsync(token, cancellationToken);

    public Task DeleteUnusedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.PasswordResetTokens.Where(t => t.UserId == userId && t.UsedAt == null).ExecuteDeleteAsync(cancellationToken);

    public async Task<Guid?> TryClaimAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default)
    {
        var claimed = await _context.Database.SqlQuery<Guid>($"""
            UPDATE password_reset_tokens SET "UsedAt" = {now}
            WHERE "TokenHash" = {tokenHash} AND "UsedAt" IS NULL AND "ExpiresAt" > {now}
            RETURNING "UserId" AS "Value"
            """).ToListAsync(cancellationToken);

        return claimed.Count == 1 ? claimed[0] : null;
    }
}
