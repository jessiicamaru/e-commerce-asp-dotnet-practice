using Ecommerce.Payment.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Payment.Infrastructure.Persistence;

/// <summary>
/// The person's payments and the refunds of them, as handed out (#217, specs/111). Every row says which provider decided
/// it - <c>Stub</c> or VNPay's sandbox move no money (specs/143) - and the export keeps saying it.
/// </summary>
public class PersonalDataReader(PaymentDbContext context) : IPersonalDataReader
{
    private readonly PaymentDbContext _context = context;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var payments = await _context.Payments.AsNoTracking().Where(p => p.UserId == userId).OrderBy(p => p.ProcessedAt)
            .Select(p => new { p.Id, p.OrderId, p.Amount, p.Currency, Status = p.Status.ToString(), p.FailureReason, p.Provider, p.ProviderReference, p.ProcessedAt })
            .ToListAsync(cancellationToken);

        // A refund names no person: it is theirs through the payment it gives back.
        var refunds = await _context.Refunds.AsNoTracking()
            .Where(r => _context.Payments.Any(p => p.Id == r.PaymentId && p.UserId == userId))
            .OrderBy(r => r.RefundedAt)
            .Select(r => new { r.Id, r.PaymentId, r.OrderId, r.Amount, r.Currency, r.Provider, r.ReturnId, r.PartId, r.RefundedAt })
            .ToListAsync(cancellationToken);

        // Each attempt to pay at a gateway (specs/143) - VNPay's page, whether or not it was used.
        var checkouts = await _context.Checkouts.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.OpenedAt)
            .Select(c => new { c.Id, c.OrderId, c.Amount, c.Currency, c.Provider, c.Reference, c.OpenedAt, c.ExpiresAt, c.CompletedAt, c.ResponseCode, c.ProviderReference })
            .ToListAsync(cancellationToken);

        return new Dictionary<string, IReadOnlyList<object>>
        {
            ["payments"] = payments.Cast<object>().ToList(),
            ["refunds"] = refunds.Cast<object>().ToList(),
            ["paymentCheckouts"] = checkouts.Cast<object>().ToList(),
        };
    }
}
