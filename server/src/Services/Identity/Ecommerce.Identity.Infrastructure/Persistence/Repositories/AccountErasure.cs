using Ecommerce.Application.Auth.Commands.DeleteAccount;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

/// <summary>Everything Identity deletes about a person when they delete their account (specs/112).</summary>
/// <remarks>⚠️ A new table that holds a person's id is declared in <c>IdentityPersonalData</c> and deleted here too -
/// the deletion test reads the export afterwards and fails on anything left in an erased section.</remarks>
public class AccountErasure(ApplicationDbContext context) : IAccountErasure
{
    private readonly ApplicationDbContext _context = context;

    public async Task EraseAsync(Guid userId, string emailKey, CancellationToken cancellationToken = default)
    {
        await _context.DeliveryAddresses.Where(a => a.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.SellerPayoutAccounts.Where(p => p.SellerId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.SellerProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ShopApplications.Where(a => a.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.OutgoingEmails.Where(e => e.RecipientId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.PasswordResetTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.BackOfficeHandoffs.Where(h => h.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.EmailConfirmationTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.TwoFactorChallenges.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.TwoFactorRecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.SignInThrottles.Where(t => t.EmailKey == emailKey).ExecuteDeleteAsync(cancellationToken);
    }
}
