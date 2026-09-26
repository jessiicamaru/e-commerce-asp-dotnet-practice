using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Order.Application.Vouchers;

/// <summary>One line of a checkout, as a voucher sees it: whose it is, what it is, what it costs.</summary>
public record VoucherLine(Guid ProductId, Guid VariantId, Guid? SellerId, decimal UnitPrice, int Quantity)
{
    public decimal Gross => UnitPrice * Quantity;
}

/// <summary>
/// What the vouchers' conditions ask about the customer - fetched only when a voucher has such a condition.
/// </summary>
/// <param name="HasBought">Any sold order before this one.</param>
/// <param name="SellersBoughtFrom">The sellers whose lines were on the customer's sold orders.</param>
public record CustomerFacts(bool HasBought, IReadOnlySet<Guid> SellersBoughtFrom)
{
    public static readonly CustomerFacts None = new(false, new HashSet<Guid>());
}

/// <summary>A voucher to apply, with how many times the caller already holds it (for the per-customer limit).</summary>
public record VoucherInput(Voucher Voucher, int CustomerUses);

/// <summary>A voucher that applied, and what it took off in the checkout's currency.</summary>
public record AppliedVoucher(Guid VoucherId, string Code, string Name, Guid? SellerId, VoucherBenefit Benefit, decimal Amount, int? PerCustomerLimit);

/// <param name="ShopDiscounts">Per line, in the order of the lines given: what the line's own seller's voucher took off.</param>
/// <param name="PlatformDiscounts">Per line: what a platform voucher took off.</param>
public record VoucherResult(
    IReadOnlyList<decimal> ShopDiscounts,
    IReadOnlyList<decimal> PlatformDiscounts,
    decimal DeliveryDiscount,
    IReadOnlyList<AppliedVoucher> Applied)
{
    public static VoucherResult None(int lines) =>
        new(Enumerable.Repeat(0m, lines).ToList(), Enumerable.Repeat(0m, lines).ToList(), 0m, []);
}

/// <summary>
/// Works out what a checkout's vouchers take off (specs/069) - in one pure function, like
/// <c>OrderTotals</c>, because it is money and every rule and every rounding deserves a test of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order:</b> each shop's voucher on that shop's lines first, then the platform's goods voucher on what is
/// left, then free delivery. <b>Stacking:</b> one per shop, one platform goods voucher, one free delivery.
/// </para>
/// <para>
/// <b>Money:</b> in the checkout's currency only, from the voucher's amount row for it - no row, not usable
/// (research D3). A voucher's discount is spread over its lines in proportion to what is left of each, in the
/// currency's minor unit, the remainder to the largest; no line is ever taken below zero.
/// </para>
/// <para>
/// Every refusal is a <see cref="ConflictException"/> naming the code: the customer asked for it, and "cannot be
/// used" with no reason is the least useful thing a checkout can say. An unknown code and a disabled one read the
/// same (research D6), so guessing learns nothing.
/// </para>
/// </remarks>
public static class VoucherPricing
{
    public const int MaxCodes = 5;

    /// <summary>What a code is stored and compared as.</summary>
    public static string Normalise(string code) => code.Trim().ToUpperInvariant();

    public static VoucherResult Apply(
        IReadOnlyList<VoucherLine> lines,
        decimal delivery,
        string currency,
        int decimals,
        IReadOnlyList<string> codes,
        IReadOnlyList<VoucherInput> found,
        CustomerFacts facts,
        DateTime now)
    {
        var shop = Enumerable.Repeat(0m, lines.Count).ToArray();
        var platform = Enumerable.Repeat(0m, lines.Count).ToArray();
        var deliveryDiscount = 0m;
        var applied = new List<AppliedVoucher>();

        var asked = codes.Select(Normalise).Where(c => c.Length > 0).Distinct().ToList();
        if (asked.Count == 0)
        {
            return VoucherResult.None(lines.Count);
        }

        if (asked.Count > MaxCodes)
        {
            throw new ConflictException($"At most {MaxCodes} vouchers can be used on one order.");
        }

        var byCode = found.ToDictionary(f => f.Voucher.Code, StringComparer.OrdinalIgnoreCase);
        var chosen = asked.Select(code => Usable(code, byCode.GetValueOrDefault(code), currency, now)).ToList();

        // Stacking (spec): one per shop, one platform voucher on the goods, one free delivery.
        foreach (var group in chosen.GroupBy(c => (c.Voucher.SellerId, Delivery: c.Voucher.Benefit == VoucherBenefit.FreeShipping)))
        {
            if (group.Count() > 1)
            {
                var names = string.Join(", ", group.Select(g => g.Voucher.Code));
                throw new ConflictException(group.Key.Delivery
                    ? $"Only one free delivery voucher can be used on an order ({names})."
                    : group.Key.SellerId is null
                        ? $"Only one shop-wide voucher can be used on an order ({names})."
                        : $"Only one voucher per shop can be used on an order ({names}).");
            }
        }

        // 1. Each shop's own voucher, on that shop's lines.
        foreach (var input in chosen.Where(c => c.Voucher.SellerId is not null).OrderBy(c => c.Voucher.Code, StringComparer.Ordinal))
        {
            var v = input.Voucher;
            var eligible = Eligible(lines, v, l => l.SellerId == v.SellerId);
            var amount = GoodsDiscount(v, input.Amount, eligible.Select(i => lines[i].Gross - shop[i]).ToList(), lines, eligible, facts, decimals);
            Allocate(amount, eligible, i => lines[i].Gross - shop[i], shop, decimals);
            applied.Add(Applied(v, amount));
        }

        // 2. The platform's voucher on the goods, on what is left after the shops'.
        foreach (var input in chosen.Where(c => c.Voucher.SellerId is null && c.Voucher.Benefit != VoucherBenefit.FreeShipping))
        {
            var v = input.Voucher;
            var eligible = Eligible(lines, v, _ => true);
            var amount = GoodsDiscount(v, input.Amount, eligible.Select(i => lines[i].Gross - shop[i]).ToList(), lines, eligible, facts, decimals);
            Allocate(amount, eligible, i => lines[i].Gross - shop[i], platform, decimals);
            applied.Add(Applied(v, amount));
        }

        // 3. Free delivery - on the delivery, with its minimum measured on what the goods now come to.
        foreach (var input in chosen.Where(c => c.Voucher.Benefit == VoucherBenefit.FreeShipping))
        {
            var v = input.Voucher;
            if (v.SellerId is not null)
            {
                throw CannotBeUsed(v.Code);   // free delivery is the platform's (research D1); creation refuses it too
            }

            if (delivery <= 0)
            {
                throw new ConflictException($"Voucher {v.Code}: this delivery is already free.");
            }

            var eligible = Eligible(lines, v, _ => true);
            var net = eligible.Select(i => lines[i].Gross - shop[i] - platform[i]).ToList();
            CheckConditions(v, input.Amount, net, lines, eligible, facts);
            deliveryDiscount = Math.Min(delivery, input.Amount.MaxDiscount ?? delivery);
            applied.Add(Applied(v, deliveryDiscount));
        }

        return new VoucherResult(shop, platform, deliveryDiscount, applied);
    }

    /// <summary>The voucher, if the code names one that can be used now in this currency - otherwise a refusal in words.</summary>
    private static (Voucher Voucher, VoucherAmount Amount) Usable(string code, VoucherInput? input, string currency, DateTime now)
    {
        if (input is null || input.Voucher.Status != VoucherStatus.Active)
        {
            throw CannotBeUsed(code);
        }

        var v = input.Voucher;
        if (now < v.StartsAt)
        {
            throw new ConflictException($"Voucher {v.Code} starts on {v.StartsAt:yyyy-MM-dd}.");
        }

        if (v.EndsAt is { } ends && now >= ends)
        {
            throw new ConflictException($"Voucher {v.Code} has expired.");
        }

        if (v.TotalLimit is { } total && v.UsedCount >= total)
        {
            throw new ConflictException($"Voucher {v.Code} has been used up.");
        }

        if (v.PerCustomerLimit is { } mine && input.CustomerUses >= mine)
        {
            throw new ConflictException($"You have already used voucher {v.Code} as many times as it allows.");
        }

        var amount = v.Amounts.FirstOrDefault(a => string.Equals(a.Currency, currency, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException($"Voucher {v.Code} cannot be used in {currency}.");

        return (v, amount);
    }

    private static ConflictException CannotBeUsed(string code) => new($"Voucher {code} cannot be used.");

    /// <summary>The lines this voucher may touch (its owner's, for a shop's) and names (its targets, when it has any).</summary>
    private static List<int> Eligible(IReadOnlyList<VoucherLine> lines, Voucher v, Func<VoucherLine, bool> mayTouch)
    {
        var eligible = Enumerable.Range(0, lines.Count)
            .Where(i => mayTouch(lines[i]) && Targets(v, lines[i]))
            .ToList();

        return eligible.Count > 0
            ? eligible
            : throw new ConflictException($"Voucher {v.Code} does not apply to anything in your cart.");
    }

    private static bool Targets(Voucher v, VoucherLine line) =>
        v.Targets.Count == 0
        || v.Targets.Any(t => t.Type switch
        {
            VoucherTargetType.Product => t.TargetId == line.ProductId,
            VoucherTargetType.Variant => t.TargetId == line.VariantId,
            _ => false,
        });

    private static decimal GoodsDiscount(
        Voucher v, VoucherAmount amount, List<decimal> bases, IReadOnlyList<VoucherLine> lines, List<int> eligible,
        CustomerFacts facts, int decimals)
    {
        CheckConditions(v, amount, bases, lines, eligible, facts);
        var available = bases.Sum();

        var discount = v.Benefit switch
        {
            VoucherBenefit.Percent => Math.Round(available * (v.Percent ?? 0m) / 100m, decimals, MidpointRounding.AwayFromZero),
            VoucherBenefit.FixedAmount => amount.FixedValue ?? 0m,
            _ => 0m,
        };

        if (v.Benefit == VoucherBenefit.Percent && amount.MaxDiscount is { } cap)
        {
            discount = Math.Min(discount, cap);
        }

        return Math.Min(discount, available);
    }

    private static void CheckConditions(
        Voucher v, VoucherAmount amount, List<decimal> bases, IReadOnlyList<VoucherLine> lines, List<int> eligible, CustomerFacts facts)
    {
        if (amount.MinSubtotal is { } minimum && bases.Sum() < minimum)
        {
            throw new ConflictException($"Voucher {v.Code} needs an order of at least {minimum:0.##} {amount.Currency} on what it applies to.");
        }

        foreach (var condition in v.Conditions)
        {
            switch (condition.Type)
            {
                case VoucherConditionType.NewCustomer when facts.HasBought:
                    throw new ConflictException($"Voucher {v.Code} is for a first order only.");
                case VoucherConditionType.FirstOrderInShop when v.SellerId is { } seller && facts.SellersBoughtFrom.Contains(seller):
                    throw new ConflictException($"Voucher {v.Code} is for a first order from this shop only.");
                case VoucherConditionType.MinQuantity when eligible.Sum(i => lines[i].Quantity) < (condition.Value ?? 0):
                    throw new ConflictException($"Voucher {v.Code} needs at least {condition.Value} items it applies to.");
            }
        }
    }

    /// <summary>
    /// Spreads <paramref name="amount"/> over the lines in proportion to what is left of each, in whole minor units,
    /// the remainder to the largest (the first of equals) - so the shares add up to the amount exactly.
    /// </summary>
    private static void Allocate(decimal amount, List<int> eligible, Func<int, decimal> left, decimal[] into, int decimals)
    {
        if (amount <= 0)
        {
            return;
        }

        var unit = 1m;
        for (var d = 0; d < decimals; d++) unit *= 10m;

        var total = eligible.Sum(left);
        var minor = decimal.Round(amount * unit, 0, MidpointRounding.AwayFromZero);
        var shares = eligible.ToDictionary(i => i, i => decimal.Floor(minor * left(i) / total));
        var remainder = minor - shares.Values.Sum();
        var largest = eligible.OrderByDescending(left).ThenBy(i => i).First();
        shares[largest] += remainder;

        foreach (var (i, share) in shares)
        {
            into[i] += share / unit;
        }
    }

    private static AppliedVoucher Applied(Voucher v, decimal amount) =>
        new(v.Id, v.Code, v.Name, v.SellerId, v.Benefit, amount, v.PerCustomerLimit);
}
