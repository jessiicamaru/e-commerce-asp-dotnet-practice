namespace Ecommerce.Order.Domain.Enums;

/// <summary>What a voucher gives (specs/069). Stored as text.</summary>
public enum VoucherBenefit
{
    /// <summary>A percentage off the lines it applies to, capped by the currency's <c>MaxDiscount</c>.</summary>
    Percent = 1,

    /// <summary>A fixed amount off, never more than the lines it applies to.</summary>
    FixedAmount,

    /// <summary>The delivery charge off, capped by <c>MaxDiscount</c>. Platform vouchers only.</summary>
    FreeShipping,
}

public enum VoucherStatus
{
    Active = 1,
    Disabled,
}

/// <summary>What must hold for a voucher to apply, beyond its dates, limits and minimum spend.</summary>
public enum VoucherConditionType
{
    /// <summary>The customer has no sold order before this one.</summary>
    NewCustomer = 1,

    /// <summary>The customer has bought nothing from this shop before (shop vouchers only).</summary>
    FirstOrderInShop,

    /// <summary>At least <c>Value</c> units on the lines the voucher applies to.</summary>
    MinQuantity,
}

public enum VoucherTargetType
{
    Product = 1,
    Variant,
}
