using Ecommerce.Inventory.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Inventory.Application.Common;

/// <summary>
/// When a sale is worth telling a seller about (#200, specs/102). No stored "low" flag: the crossing is judged from the
/// available stock before and after the sale, read under the reservation's row lock (research D2) - so a return above
/// the line makes the next fall a new crossing, and staying below never crosses again.
/// </summary>
public static class LowStock
{
    /// <summary>Went from at or above the line to strictly below it. A line of 0 is "never tell".</summary>
    public static bool Crossed(int before, int after, int threshold) =>
        threshold > 0 && before >= threshold && after < threshold;
}

/// <summary>The shop's default line (<c>Inventory:LowStock:DefaultThreshold</c>), read once at startup.</summary>
public record LowStockSettings(int DefaultThreshold)
{
    public const string Key = "Inventory:LowStock:DefaultThreshold";
    public const int Default = 5;
    public const int Max = 100_000;

    /// <summary>A variant's own line, or the shop's when it chose none.</summary>
    public int For(StockItem stock) => stock.LowStockThreshold ?? DefaultThreshold;

    /// <summary>Refuses a value it cannot use rather than reading a typo as "never tell".</summary>
    public static LowStockSettings From(IConfiguration configuration)
    {
        var raw = configuration[Key];
        if (string.IsNullOrWhiteSpace(raw)) return new LowStockSettings(Default);
        if (!int.TryParse(raw, out var value) || value < 0 || value > Max)
            throw new InvalidOperationException($"{Key} must be a whole number from 0 to {Max}; it is '{raw}'.");
        return new LowStockSettings(value);
    }
}
