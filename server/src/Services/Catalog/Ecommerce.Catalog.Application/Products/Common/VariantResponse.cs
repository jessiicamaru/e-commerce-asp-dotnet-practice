using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Products.Common;

/// <summary>One shape a product is sold in, as a shopper sees it (specs/020).</summary>
/// <param name="OptionSummary">
/// The options in words - <c>Kit: Body only · Colour: Black</c> - and empty for a product sold in one
/// shape. This is the string frozen onto an order line.
/// </param>
/// <param name="Availability">
/// <c>"InStock"</c> / <c>"OutOfStock"</c> for THIS variant, never a count: a read model fed by
/// Inventory, which nothing may sell against.
/// </param>
public record VariantResponse(
    Guid Id,
    string Sku,
    decimal Price,
    string OptionSummary,
    List<VariantOptionResponse> Options,
    string Availability,
    bool IsActive
)
{
    public static VariantResponse From(ProductVariant variant) => new(
        variant.Id,
        variant.Sku,
        variant.Price,
        variant.OptionSummary,
        variant.Options.Select(option => new VariantOptionResponse(option.Name, option.Value)).ToList(),
        ProductAvailability.From(variant.Availability),
        variant.IsActive);
}

public record VariantOptionResponse(string Name, string Value);
