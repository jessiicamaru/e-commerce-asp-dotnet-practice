using Ecommerce.Order.Domain.Entities;

namespace Ecommerce.Order.Application.Vouchers;

/// <summary>A voucher as its owner sees it in a list (specs/069).</summary>
public record VoucherSummary(
    Guid Id,
    string Code,
    string Name,
    bool IsPlatform,
    string Benefit,
    decimal? Percent,
    string Status,
    DateTime StartsAt,
    DateTime? EndsAt,
    int? TotalLimit,
    int UsedCount,
    int? PerCustomerLimit,
    List<VoucherAmountResponse> Amounts,
    List<VoucherConditionResponse> Conditions,
    List<VoucherTargetResponse> Targets,
    DateTime CreatedAt);

public record VoucherAmountResponse(string Currency, decimal? FixedValue, decimal? MaxDiscount, decimal? MinSubtotal);

public record VoucherConditionResponse(string Type, int? Value);

public record VoucherTargetResponse(string Type, Guid Id);

public enum DisableOutcome { NotFound, AlreadyDisabled, Disabled }

public interface IVoucherRepository
{
    /// <summary>The vouchers these codes name, with their parts, and how many orders the customer holds each on.</summary>
    Task<List<VoucherInput>> FindForCheckoutAsync(IReadOnlyCollection<string> codes, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Whether the customer has a sold order, and from which sellers - what the conditions ask.</summary>
    Task<CustomerFacts> CustomerFactsAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// In ONE transaction: claims a use of every applied voucher - the total and the customer's, each a guarded
    /// statement (research D5) - then saves everything staged on the context (the order, its redemptions, the
    /// outbox message, the audit entry) and commits. Returns the code that could not be claimed, having saved
    /// nothing, or null.
    /// </summary>
    Task<string?> ClaimAndSaveAsync(IReadOnlyList<AppliedVoucher> applied, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives an order's uses back - once: one statement marks its redemptions released and takes one off each
    /// voucher's and the customer's counters. Runs in the caller's transaction (the settle or cancel stage).
    /// </summary>
    Task ReleaseForOrderAsync(Guid orderId, DateTime at, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default);

    /// <summary>Saves a new voucher with its audit entry; false when the code was taken meanwhile (the unique index decides).</summary>
    Task<bool> TrySaveNewAsync(CancellationToken cancellationToken = default);

    /// <summary>The platform's vouchers (<paramref name="sellerId"/> null) or one seller's, newest first.</summary>
    Task<(List<VoucherSummary> Items, int TotalCount)> GetPageAsync(Guid? sellerId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// One guarded statement from Active to Disabled. <paramref name="ownerSellerId"/> limits it to that seller's
    /// voucher (any other is <see cref="DisableOutcome.NotFound"/>); null is staff, who may disable any.
    /// </summary>
    /// <param name="stage">Stages the audit entry, inside the same transaction, only when this call disabled it.</param>
    Task<(DisableOutcome Outcome, Voucher? Voucher)> TryDisableAsync(
        Guid id, Guid? ownerSellerId, bool staff, DateTime at, Func<Voucher, CancellationToken, Task> stage, CancellationToken cancellationToken = default);
}
