using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Common.Interfaces;

namespace Ecommerce.Cart.Application.Carts;

/// <summary>A line to price: what was chosen and how many - never a price, which is Catalog's (specs/009).</summary>
/// <param name="VariantId">The shape; null for a line from before variants, addressed by its product (specs/020).</param>
public record CartLineInput(Guid ProductId, Guid? VariantId, int Quantity)
{
    /// <summary>What the line is matched and priced by: the variant, or the product for an older line.</summary>
    public Guid SellableId => VariantId ?? ProductId;
}

/// <summary>
/// Prices cart lines from Catalog, in the request's language and currency - the stored cart's and the browser's alike
/// (specs/162 research D1), so the two cannot read differently.
/// </summary>
public static class CartPricing
{
    public static async Task<CartResponse> PriceAsync(
        IReadOnlyList<CartLineInput> lines,
        ICatalogProducts catalog,
        string language,
        string currency,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return new CartResponse([], 0m, CanCheckOut: false, PricesAvailable: true, currency);
        }

        // Described by the SELLABLE unit: a variant carries the price and the words for what it is.
        // In the language AND the currency this request is in. Sending one and not the other is
        // exactly the bug specs/021 shipped with: the product page read Vietnamese while the cart
        // read English, because Cart never passed a language.
        var described = await catalog.DescribeAsync(
            lines.Select(l => l.SellableId).Distinct().ToList(),
            cancellationToken,
            language,
            currency);

        var byId = described.Products.ToDictionary(p => p.VariantId == default ? p.ProductId : p.VariantId);
        var missing = described.Missing.ToHashSet();

        var result = new List<CartLineResponse>();

        foreach (var line in lines)
        {
            if (!described.Reachable)
            {
                result.Add(new CartLineResponse(
                    line.ProductId, null, line.Quantity, null, null, CartLineStatus.PriceUnavailable,
                    line.SellableId));
            }
            else if (missing.Contains(line.SellableId) || !byId.TryGetValue(line.SellableId, out var product))
            {
                // Kept, and marked. Silently dropping it is the worst option.
                result.Add(new CartLineResponse(
                    line.ProductId, null, line.Quantity, null, null, CartLineStatus.NoLongerAvailable,
                    line.SellableId));
            }
            else if (product.Price is null)
            {
                // Not withdrawn - just not priced in the currency being browsed in. Switching
                // currency brings it back, which "not for sale" would not lead anyone to try.
                result.Add(new CartLineResponse(
                    product.ProductId, product.Name, line.Quantity, null, null,
                    CartLineStatus.NotSoldInCurrency, line.SellableId, product.OptionSummary));
            }
            else if (!product.Sellable)
            {
                result.Add(new CartLineResponse(
                    product.ProductId, product.Name, line.Quantity, product.Price, null, CartLineStatus.NotForSale,
                    line.SellableId, product.OptionSummary));
            }
            else
            {
                result.Add(new CartLineResponse(
                    product.ProductId, product.Name, line.Quantity, product.Price,
                    product.Price.Value * line.Quantity, CartLineStatus.Available,
                    line.SellableId, product.OptionSummary));
            }
        }

        var canCheckOut = described.Reachable
            && result.All(l => l.Status == CartLineStatus.Available);

        var estimate = described.Reachable
            ? result.Where(l => l.LineTotal.HasValue).Sum(l => l.LineTotal!.Value)
            : (decimal?)null;

        return new CartResponse(result, estimate, canCheckOut, described.Reachable, currency);
    }
}
