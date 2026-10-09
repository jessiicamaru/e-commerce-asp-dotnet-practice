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

        // Priced by the code that prices the browser's cart too (specs/162), so the two cannot read differently.
        var lines = cart.Lines.OrderBy(l => l.AddedAt)
            .Select(l => new CartLineInput(l.ProductId, l.VariantId, l.Quantity))
            .ToList();
        return await CartPricing.PriceAsync(lines, _catalog, language.Current, currency.Current.Code, cancellationToken);
    }
}
