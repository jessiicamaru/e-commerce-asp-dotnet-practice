using Ecommerce.Application.Auth.Commands.EmailConfirmation;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class EmailConfirmationRepository(ApplicationDbContext context) : IEmailConfirmationRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task AddAsync(EmailConfirmationToken token, CancellationToken cancellationToken = default) =>
        await _context.EmailConfirmationTokens.AddAsync(token, cancellationToken);

    public Task DeleteUnusedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.EmailConfirmationTokens.Where(t => t.UserId == userId && t.UsedAt == null).ExecuteDeleteAsync(cancellationToken);

    public async Task<bool> SentSinceAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
    {
        // The person's row, locked until the transaction ends: a second resend waits here, then sees the first.
        await _context.Database.SqlQuery<Guid>(
            $"""SELECT "Id" AS "Value" FROM users WHERE "Id" = {userId} FOR UPDATE""").ToListAsync(cancellationToken);

        return await _context.EmailConfirmationTokens.AnyAsync(t => t.UserId == userId && t.CreatedAt > since, cancellationToken);
    }

    public async Task<Guid?> TryClaimAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default)
    {
        var claimed = await _context.Database.SqlQuery<Guid>($"""
            UPDATE email_confirmation_tokens SET "UsedAt" = {now}
            WHERE "TokenHash" = {tokenHash} AND "UsedAt" IS NULL AND "ExpiresAt" > {now}
            RETURNING "UserId" AS "Value"
            """).ToListAsync(cancellationToken);

        return claimed.Count == 1 ? claimed[0] : null;
    }

    public async Task<bool> TryConfirmAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default) =>
        await _context.Users
            .Where(u => u.Id == userId && u.EmailConfirmedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmedAt, now).SetProperty(u => u.UpdatedAt, now), cancellationToken) == 1;
}
