using System.Globalization;
using Ecommerce.Order.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Order.Infrastructure.Marketplace;

/// <summary>
/// <c>Marketplace:CommissionRate</c> from configuration, checked once at startup like <c>Tax</c>: it must
/// be there and be at least 0 and below 1, or Order does not start (specs/037 FR-008). A missing rate is
/// not read as zero - that would quietly give every sale away.
/// </summary>
public class ConfiguredCommissionRate : ICommissionRate
{
    public ConfiguredCommissionRate(IConfiguration configuration)
    {
        var raw = configuration["Marketplace:CommissionRate"];

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
        {
            throw new InvalidOperationException(
                "Marketplace:CommissionRate is not configured. Checkout cannot record what a seller is owed.");
        }

        if (rate < 0 || rate >= 1)
        {
            throw new InvalidOperationException(
                $"Marketplace:CommissionRate is {rate}; it must be at least 0 and below 1.");
        }

        Current = rate;
    }

    public decimal Current { get; }
}
