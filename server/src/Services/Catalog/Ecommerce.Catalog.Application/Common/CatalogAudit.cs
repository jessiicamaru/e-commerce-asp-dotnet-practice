using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common;

/// <summary>What an audit entry keeps of a product or a variant (specs/041) - the fields a person edits.</summary>
public static class CatalogAudit
{
    public static object Of(Product p) => new
    {
        p.Name,
        p.Sku,
        p.Description,
        p.CategoryId,
        p.SellerId,
        p.IsActive,
        p.Price
    };

    public static object Of(ProductVariant v) => new
    {
        v.Sku,
        v.OptionSummary,
        v.Price,
        v.IsActive,
        Prices = v.Prices.OrderBy(x => x.Currency).ToDictionary(x => x.Currency, x => x.Amount)
    };
}
