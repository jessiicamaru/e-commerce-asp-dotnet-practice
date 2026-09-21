using Ecommerce.Order.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Order.Infrastructure.Tax;

/// <summary>
/// Tax rates from configuration - <c>Tax:DefaultRate</c> and <c>Tax:Rates:{COUNTRY}</c> - checked once,
/// at startup: a default must exist and every rate must be at least 0 and below 1 (specs/012 FR-010).
/// </summary>
public class ConfiguredTaxRates : ITaxRates
{
    private readonly decimal _default;
    private readonly Dictionary<string, decimal> _rates;

    public ConfiguredTaxRates(IConfiguration configuration)
    {
        var section = configuration.GetSection("Tax");

        if (!decimal.TryParse(section["DefaultRate"], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out _default))
        {
            throw new InvalidOperationException("Tax:DefaultRate is not configured. Checkout cannot compute tax.");
        }

        _rates = section.GetSection("Rates").GetChildren().ToDictionary(
            c => c.Key.Trim().ToUpperInvariant(),
            c => decimal.TryParse(c.Value, System.Globalization.NumberStyles.Number,
                     System.Globalization.CultureInfo.InvariantCulture, out var r)
                ? r
                : throw new InvalidOperationException($"Tax rate for '{c.Key}' is not a number."));

        foreach (var (country, rate) in _rates.Append(new KeyValuePair<string, decimal>("default", _default)))
        {
            if (rate < 0 || rate >= 1)
            {
                throw new InvalidOperationException($"Tax rate for '{country}' is {rate}; it must be at least 0 and below 1.");
            }
        }
    }

    public decimal RateFor(string country) =>
        _rates.TryGetValue(country.Trim().ToUpperInvariant(), out var rate) ? rate : _default;
}
