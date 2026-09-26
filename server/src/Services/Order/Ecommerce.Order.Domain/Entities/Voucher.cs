using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Domain.Entities;

/// <summary>
/// A discount a customer applies with a code (specs/069, #108): a platform voucher when <see cref="SellerId"/>
/// is null - on anything, paid for by the shop - or a seller's own, on their lines only and paid for out of
/// their payout.
/// </summary>
/// <remarks>
/// A voucher is composed rather than special-cased: WHAT it gives (<see cref="Benefit"/>), what it applies to
/// (<see cref="Targets"/>, none meaning everything it may touch), what must be true (<see cref="Conditions"/>),
/// and its amounts per currency (<see cref="Amounts"/>) - never converted between currencies (specs/022).
/// </remarks>
public class Voucher
{
    public Guid Id { get; set; }

    /// <summary>What the customer types. Stored upper-case; unique across the shop.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Whose: null is the platform's, otherwise the seller whose lines it applies to.</summary>
    public Guid? SellerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public VoucherBenefit Benefit { get; set; }

    /// <summary>1-100, for <see cref="VoucherBenefit.Percent"/> only.</summary>
    public decimal? Percent { get; set; }

    public DateTime StartsAt { get; set; }

    /// <summary>Exclusive; null never ends.</summary>
    public DateTime? EndsAt { get; set; }

    public VoucherStatus Status { get; set; } = VoucherStatus.Active;

    /// <summary>How many orders may use it in all; null for no limit.</summary>
    public int? TotalLimit { get; set; }

    /// <summary>How many orders hold it now - claimed at checkout, given back by a failed or cancelled order.</summary>
    public int UsedCount { get; set; }

    /// <summary>How many orders one customer may use it on; null for no limit.</summary>
    public int? PerCustomerLimit { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<VoucherCondition> Conditions { get; set; } = [];
    public List<VoucherTarget> Targets { get; set; } = [];
    public List<VoucherAmount> Amounts { get; set; } = [];
}

/// <summary>One thing that must hold for the voucher to apply. All of a voucher's conditions must.</summary>
public class VoucherCondition
{
    public Guid VoucherId { get; set; }
    public VoucherConditionType Type { get; set; }

    /// <summary>The condition's number, where it has one (<see cref="VoucherConditionType.MinQuantity"/>).</summary>
    public int? Value { get; set; }
}

/// <summary>A product or variant the voucher applies to. A voucher with no targets applies to every line it may touch.</summary>
public class VoucherTarget
{
    public Guid VoucherId { get; set; }
    public VoucherTargetType Type { get; set; }
    public Guid TargetId { get; set; }
}

/// <summary>
/// The voucher's money in one currency. A voucher is usable only in a currency it has one of these for, even a
/// percentage - its cap and minimum are amounts (specs/069 research D3).
/// </summary>
public class VoucherAmount
{
    public Guid VoucherId { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>What a <see cref="VoucherBenefit.FixedAmount"/> voucher takes off.</summary>
    public decimal? FixedValue { get; set; }

    /// <summary>The most a percentage or free delivery takes off; null for no cap.</summary>
    public decimal? MaxDiscount { get; set; }

    /// <summary>The least the lines it applies to must come to; null for none.</summary>
    public decimal? MinSubtotal { get; set; }
}

/// <summary>How many orders one customer holds the voucher on - the counter the per-customer limit guards.</summary>
public class VoucherCustomerUse
{
    public Guid VoucherId { get; set; }
    public Guid CustomerId { get; set; }
    public int Uses { get; set; }
}

/// <summary>
/// A voucher used on an order, frozen with the order: its code and what it took off, so disabling or deleting the
/// voucher later changes no order. <see cref="ReleasedAt"/> is set when the order failed or was cancelled and the
/// use was given back.
/// </summary>
public class VoucherRedemption
{
    public Guid Id { get; set; }
    public Guid VoucherId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? SellerId { get; set; }
    public VoucherBenefit Benefit { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
}
