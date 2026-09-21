namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// The ways an order can be sent, and what each costs. Owned by Order, because Order prices checkout.
/// </summary>
public interface IShippingOptions
{
    IReadOnlyList<ShippingOption> All { get; }

    /// <summary>Case-insensitive; <c>null</c> when there is no such option.</summary>
    ShippingOption? Find(string? code);
}

public record ShippingOption(string Code, string Name, decimal Price);
