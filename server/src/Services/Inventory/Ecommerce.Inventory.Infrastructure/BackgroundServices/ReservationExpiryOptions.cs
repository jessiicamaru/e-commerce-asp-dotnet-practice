namespace Ecommerce.Inventory.Infrastructure.BackgroundServices;

public class ReservationExpiryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>How long units stay held before the sweeper may reclaim them.</summary>
    public int ReservationTtlMinutes { get; set; } = 15;

    public int SweepIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 100;
}
