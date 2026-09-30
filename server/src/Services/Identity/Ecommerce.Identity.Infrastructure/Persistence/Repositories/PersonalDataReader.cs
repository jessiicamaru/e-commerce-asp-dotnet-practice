using Ecommerce.Application.MyData;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

/// <summary>
/// The person's rows, as handed out (#217, specs/111). ⚠️ Projections, never entities: the password hash, the TOTP
/// secret, a payout account's full number and an email's data are not in any of them.
/// </summary>
public class PersonalDataReader(ApplicationDbContext context) : IPersonalDataReader
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.Language, u.CreatedAt, u.UpdatedAt,
                u.EmailConfirmedAt, TwoFactorEnabledAt = u.TwoFactorEnabledAt,
                u.LockedUntil, u.LockReason, u.BannedAt, u.BanReason,
                Roles = u.Roles.Select(r => r.Name).ToList(),
            })
            .ToListAsync(cancellationToken);

        var addresses = await _context.DeliveryAddresses.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.RecipientName, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country, a.Phone, a.IsDefault, a.CreatedAt, a.UpdatedAt })
            .ToListAsync(cancellationToken);

        var shop = await _context.SellerProfiles.AsNoTracking().Where(s => s.UserId == userId)
            .Select(s => new { s.ShopName, s.Description, s.CreatedAt, s.UpdatedAt })
            .ToListAsync(cancellationToken);

        // Masked, as on the seller's own page (specs/106): a downloaded file is easier to lose than a session.
        var payout = (await _context.SellerPayoutAccounts.AsNoTracking().Where(p => p.SellerId == userId).ToListAsync(cancellationToken))
            .Select(p => new { p.BankName, p.AccountHolder, AccountNumber = p.Masked, p.UpdatedAt })
            .ToList();

        var applications = await _context.ShopApplications.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.ShopName, a.Description, a.Phone, Status = a.Status.ToString(), a.DecisionReason, a.DecidedAt, a.CreatedAt })
            .ToListAsync(cancellationToken);

        // That an email was sent, and what kind - never its data, which may hold a link or another person's words.
        var emails = await _context.OutgoingEmails.AsNoTracking().Where(e => e.RecipientId == userId).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Template, e.Language, Status = e.Status.ToString(), e.CreatedAt, e.SentAt })
            .ToListAsync(cancellationToken);

        return new Dictionary<string, IReadOnlyList<object>>
        {
            ["profile"] = profile.Cast<object>().ToList(),
            ["addresses"] = addresses.Cast<object>().ToList(),
            ["shop"] = shop.Cast<object>().ToList(),
            ["payoutAccount"] = payout.Cast<object>().ToList(),
            ["shopApplications"] = applications.Cast<object>().ToList(),
            ["emails"] = emails.Cast<object>().ToList(),
        };
    }
}
