using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using MediatR;

namespace Ecommerce.Cart.Application.Carts.Queries.GetMyCart;

/// <summary>
/// The caller's cart, priced by Catalog as it stands right now.
/// </summary>
/// <remarks>
/// The price shown is fetched, not remembered. The cart stores none, so there is nothing stale to
/// charge by accident - and the amount charged is decided at checkout regardless.
/// </remarks>
public class GetMyCartQueryHandler(
    ICartRepository carts,
    ICatalogProducts catalog,
    ICurrentUser currentUser,
    IRequestLanguage language,
    IRequestCurrency currency) : IRequestHandler<GetMyCartQuery, CartResponse>
{
    private readonly ICartRepository _carts = carts;
    private readonly ICatalogProducts _catalog = catalog;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<CartResponse> Handle(GetMyCartQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var cart = await _carts.GetAsync(userId, cancellationToken);

        // A customer who has never added anything has an empty cart, not a missing one.
        if (cart is null || cart.Lines.Count == 0)
        {
            return new CartResponse(
                [], 0m, CanCheckOut: false, PricesAvailable: true, currency.Current.Code);
        }

        var lines = cart.Lines.OrderBy(l => l.AddedAt).ToList();
        // Described by the SELLABLE unit: a variant carries the price and the words for what it is.
        // In the language AND the currency this request is in. Sending one and not the other is
        // exactly the bug specs/021 shipped with: the product page read Vietnamese while the cart
        // read English, because Cart never passed a language.
        var described = await _catalog.DescribeAsync(
            lines.Select(l => l.SellableId).Distinct().ToList(),
            cancellationToken,
            language.Current,
            currency.Current.Code);

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

        return new CartResponse(
            result, estimate, canCheckOut, described.Reachable, currency.Current.Code);
    }
}
