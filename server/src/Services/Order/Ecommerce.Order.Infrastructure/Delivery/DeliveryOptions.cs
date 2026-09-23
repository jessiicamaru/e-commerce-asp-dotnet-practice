using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Infrastructure.Delivery;

/// <summary>
/// <c>Delivery</c> in configuration (specs/040): how long after shipping a parcel nobody confirmed is taken
/// as delivered, and how often to look.
/// </summary>
public class DeliveryOptions
{
    public const string SectionName = "Delivery";

    /// <summary>Days after shipping. At least 1 - zero would pay a seller the moment they click "shipped".</summary>
    public int AutoConfirmDays { get; set; } = 7;

    public int SweepIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Binds and validates at startup: a nonsensical period stops Order rather than paying sellers early or
    /// never (FR-004).
    /// </summary>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DeliveryOptions>()
            .Bind(configuration.GetSection(SectionName))
            .Validate(o => o.AutoConfirmDays >= 1, "Delivery:AutoConfirmDays must be at least 1.")
            .Validate(o => o.SweepIntervalMinutes >= 1, "Delivery:SweepIntervalMinutes must be at least 1.")
            .ValidateOnStart();
    }
}
