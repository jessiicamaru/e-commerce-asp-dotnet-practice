using Ecommerce.Order.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Order.Infrastructure.Shipping;

/// <summary>
/// Delivery options from configuration (<c>Shipping:Options</c>), checked once, at startup.
/// </summary>
/// <remarks>
/// A misconfiguration - no options, a duplicate code, a negative price - stops the service from
/// starting, rather than surfacing as a strange checkout (constitution: fail at startup, not per
/// request). Configuration rather than a table: two rows that rarely change do not need screens.
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

            if (o.Price < 0)
            {
                throw new InvalidOperationException($"Delivery option '{o.Code}' has a negative price.");
            }

            return new ShippingOption(o.Code.Trim().ToLowerInvariant(), o.Name.Trim(), o.Price);
        }).ToList();
    }

    public IReadOnlyList<ShippingOption> All { get; }

    public ShippingOption? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(o => string.Equals(o.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    private sealed class ShippingOptionSetting
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }
}
