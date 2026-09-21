using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Shared.Authentication;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Cart.WebApi.Grpc;

/// <summary>
/// Tells Order what is in the caller's cart, at checkout.
/// </summary>
/// <remarks>
/// <b>The caller is identified by the forwarded token, not by anything in the request.</b> Order
/// sends the customer's bearer token in the call's metadata and this reads the user through
/// ICurrentUser - the same way every REST endpoint here does. The request message is empty on purpose:
/// GetCart(user_id) would let anything on the network read anybody's cart by naming them.
/// </remarks>
[Authorize]
public class CartReadingService(
    ICartRepository carts,
    ICurrentUser currentUser) : CartReading.CartReadingBase
{
    private readonly ICartRepository _carts = carts;
    private readonly ICurrentUser _currentUser = currentUser;

    public override async Task<GetMyCartResponse> GetMyCart(GetMyCartRequest request, ServerCallContext context)
    {
        var userId = _currentUser.Id
            ?? throw new RpcException(new Status(
                StatusCode.Unauthenticated, "The forwarded token does not carry a valid user id."));

        var cart = await _carts.GetAsync(userId, context.CancellationToken);
        var response = new GetMyCartResponse();

        // An empty cart is an answer, not an error. Order decides that checking it out is refused.
        foreach (var line in cart?.Lines.OrderBy(l => l.AddedAt) ?? Enumerable.Empty<Domain.Entities.CartLine>())
        {
            response.Items.Add(new CartItem { ProductId = line.ProductId.ToString(), Quantity = line.Quantity });
        }

        return response;
    }
}
