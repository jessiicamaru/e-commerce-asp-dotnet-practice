using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Money;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Order.Infrastructure.Shipping;

/// <summary>
/// Delivery options from configuration (<c>Shipping:Options</c>), checked once, at startup.
/// </summary>
/// <remarks>
/// A misconfiguration - no options, a duplicate code, a negative price, nothing priced in the shop's
/// own currency - stops the service from starting, rather than surfacing as a strange checkout
/// (constitution: fail at startup, not per request). Configuration rather than a table: two rows that
/// rarely change do not need screens.
/// </remarks>
public class ConfiguredShippingOptions : IShippingOptions
{
    public ConfiguredShippingOptions(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Shipping:Options").Get<List<ShippingOptionSetting>>()
            ?? [];

        if (configured.Count == 0)
        {
            throw new InvalidOperationException(
                "No delivery options are configured (Shipping:Options). Checkout cannot price delivery.");
        }

        var duplicate = configured
            .GroupBy(o => o.Code?.Trim().ToLowerInvariant())
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Delivery option '{duplicate.Key}' is configured twice.");
        }

        All = configured.Select(o =>
        {
            if (string.IsNullOrWhiteSpace(o.Code) || string.IsNullOrWhiteSpace(o.Name))
            {
                throw new InvalidOperationException("Every delivery option needs a Code and a Name.");
            }

            if (o.Prices is null || o.Prices.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Delivery option '{o.Code}' has no prices. Give it one per currency it is offered in, "
                    + "e.g. \"Prices\": { \"VND\": 30000, \"USD\": 2 }.");
            }

            var negative = o.Prices.FirstOrDefault(price => price.Value < 0);

            if (negative.Key is not null)
            {
                throw new InvalidOperationException(
                    $"Delivery option '{o.Code}' has a negative price in {negative.Key}.");
            }

            return new ShippingOption(
                o.Code.Trim().ToLowerInvariant(),
                o.Name.Trim(),
                o.Prices.ToDictionary(price => price.Key.Trim().ToUpperInvariant(), price => price.Value));
        }).ToList();

        // A shop that cannot deliver anything in its own currency cannot take an order at all, and
        // that is worth finding out at startup rather than at the first checkout.
        var defaultCurrency = (configuration[$"{CurrencyOptions.SectionName}:DefaultCurrency"] ?? "VND")
            .ToUpperInvariant();

        if (!All.Any(option => option.PriceIn(defaultCurrency) is not null))
        {
            throw new InvalidOperationException(
                $"No delivery option has a price in '{defaultCurrency}', the shop's default currency. "
                + "No order could be placed.");
        }
    }

    public IReadOnlyList<ShippingOption> All { get; }

    public ShippingOption? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(o => string.Equals(o.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ShippingOption> Offered(string currency) =>
        All.Where(option => option.PriceIn(currency) is not null).ToList();

    private sealed class ShippingOptionSetting
    {
        public string? Code { get; set; }
        public string? Name { get; set; }

        /// <summary>One amount per currency code. Keyed by code so a third currency is a key.</summary>
        public Dictionary<string, decimal>? Prices { get; set; }
    }
}
