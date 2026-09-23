namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// The marketplace's commission on a seller's goods (specs/037): one rate for every seller, frozen onto
/// each order at checkout so a rate changed tomorrow never rewrites a sale made today.
/// </summary>
public interface ICommissionRate
{
    /// <summary>At least 0 and below 1, e.g. 0.10.</summary>
    decimal Current { get; }
}
