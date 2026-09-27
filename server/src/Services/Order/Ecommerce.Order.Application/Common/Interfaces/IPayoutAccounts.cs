namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>Where a payout goes, as it may be frozen on it - never the whole account number (specs/106 research D2).</summary>
public record PayoutDestination(string Bank, string Holder, string Last4, DateTime UpdatedAt);

/// <summary>
/// A seller's payout account, asked of Identity when an administrator records a payout (specs/106) - with the
/// administrator's own token, which is what Identity authorizes. Null when the seller has given none.
/// </summary>
public interface IPayoutAccounts
{
    Task<PayoutDestination?> GetAsync(Guid sellerId, CancellationToken cancellationToken = default);
}
