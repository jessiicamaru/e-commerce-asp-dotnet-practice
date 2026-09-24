namespace Ecommerce.Orchestrator.WebApi.Timeouts;

/// <summary>
/// How long the saga waits for Payment once stock is reserved, and how often it looks (specs/053).
/// </summary>
/// <remarks>
/// ⚠️ The timeout plus one sweep must be SHORTER than Inventory's hold
/// (<c>INVENTORY_RESERVATION_TTL_MINUTES</c>): the point is to fail the order while its stock is still
/// held, so releasing it is immediate and nothing is paid for after it went back on the shelf. When both are
/// visible - the same <c>.env</c>, the same compose file - a timeout that is not shorter refuses to start.
/// </remarks>
public sealed record PaymentTimeoutOptions(TimeSpan Timeout, TimeSpan SweepInterval)
{
    public const string TimeoutVariable = "ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS";
    public const string SweepVariable = "ORCHESTRATOR_TIMEOUT_SWEEP_SECONDS";
    public const string InventoryHoldVariable = "INVENTORY_RESERVATION_TTL_MINUTES";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromSeconds(30);

    /// <summary>Reads and checks the settings; throws, naming each problem, rather than start wrong.</summary>
    public static PaymentTimeoutOptions From(Func<string, string?> read)
    {
        var problems = new List<string>();
        var timeout = Seconds(read(TimeoutVariable), TimeoutVariable, DefaultTimeout, problems);
        var sweep = Seconds(read(SweepVariable), SweepVariable, DefaultSweepInterval, problems);

        var hold = read(InventoryHoldVariable);
        if (!string.IsNullOrWhiteSpace(hold))
        {
            if (!double.TryParse(hold, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minutes) || minutes <= 0)
            {
                problems.Add($"{InventoryHoldVariable} must be a positive number of minutes, not '{hold}'.");
            }
            else if (timeout + sweep >= TimeSpan.FromMinutes(minutes))
            {
                problems.Add(
                    $"{TimeoutVariable} ({timeout.TotalSeconds:0}s) plus one sweep ({sweep.TotalSeconds:0}s) must be shorter than "
                    + $"Inventory's hold, {InventoryHoldVariable} ({minutes} min). Otherwise the stock goes back on the shelf "
                    + "before the saga stops waiting, and a late payment is taken for stock nobody holds (#123).");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The payment timeout is misconfigured: " + string.Join(" ", problems));
        }

        return new PaymentTimeoutOptions(timeout, sweep);
    }

    private static TimeSpan Seconds(string? value, string name, TimeSpan fallback, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (int.TryParse(value, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        problems.Add($"{name} must be a positive whole number of seconds, not '{value}'.");
        return fallback;
    }
}
