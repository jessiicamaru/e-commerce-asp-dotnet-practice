using Ecommerce.Order.Application.Orders.Common;

namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// What the shop owes each seller, and the payouts that settle it (specs/037). Every read counts only
/// paid orders (<see cref="Sales.Statuses"/>) and only parts whose terms were recorded at checkout.
/// </summary>
public interface IPayoutRepository
{
    /// <summary>One row per currency the seller has earnings in.</summary>
    Task<List<BalanceResponse>> GetBalanceAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>A page of the seller's payouts, newest first.</summary>
    Task<(List<PayoutResponse> Payouts, int TotalCount)> GetPayoutsPageAsync(
        Guid sellerId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Every seller and currency with something due now.</summary>
    Task<List<PayoutDueResponse>> GetDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims every part due to <paramref name="sellerId"/> in <paramref name="currency"/> for a new
    /// payout and records it, in ONE statement - or returns null when nothing is due.
    /// </summary>
    /// <remarks>
    /// ⚠️ The claim is the guard (research D5): <c>WHERE "PayoutId" IS NULL</c> is evaluated by the
    /// database under the row lock, so of two payouts recorded at once the second claims nothing and
    /// records nothing. The amount is summed over what THIS statement claimed - never read first.
    /// </remarks>
    Task<PayoutResponse?> TryRecordAsync(
        Guid payoutId,
        Guid sellerId,
        string currency,
        Guid recordedBy,
        DateTime at,
        CancellationToken cancellationToken = default);
}
