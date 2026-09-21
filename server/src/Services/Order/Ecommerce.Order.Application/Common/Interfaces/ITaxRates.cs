namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// The rate of tax for a destination. Owned by Order, which decides what is charged (specs/012 D2).
/// </summary>
public interface ITaxRates
{
    /// <summary>The configured rate for this ISO country code, or the default rate.</summary>
    decimal RateFor(string country);
}
