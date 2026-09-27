namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What the shop owes sellers (specs/037).</summary>
public static class Payouts
{
    /// <summary>The refusal when a payout would cover nothing - the same words for "never sold" and "all paid".</summary>
    public static string NothingDue(string currency) => $"Nothing is due to this seller in {currency}.";

    /// <summary>The refusal when a seller has given no payout account (specs/106).</summary>
    public const string NoAccount = "This seller has given no payout account; there is nowhere to pay.";
}

/// <summary>
/// A seller's money in one currency. Only paid orders count, and only parts whose terms were recorded
/// at checkout (research D6).
/// </summary>
/// <param name="OnTheWay">Paid by the customer, their part not shipped yet.</param>
/// <param name="Due">Shipped, not paid out yet.</param>
/// <param name="PaidOut">Covered by a payout.</param>
public record BalanceResponse(string Currency, decimal OnTheWay, decimal Due, decimal PaidOut);

/// <summary>One settlement of what was due to one seller in one currency.</summary>
/// <param name="PaidToAccountLast4">Where it went (specs/106): null on payouts from before.</param>
public record PayoutResponse(
    Guid Id, Guid SellerId, string Currency, decimal Amount, int PartCount, DateTime CreatedAt,
    string? PaidToBank = null, string? PaidToHolder = null, string? PaidToAccountLast4 = null);

/// <summary>What is due now to one seller in one currency - an administrator's worklist.</summary>
/// <param name="SellerName">The most recent name frozen on the seller's lines (specs/036), or null.</param>
/// <param name="Parts">How many shipped parts the amount covers.</param>
public record PayoutDueResponse(Guid SellerId, string? SellerName, string Currency, decimal Due, int Parts);
