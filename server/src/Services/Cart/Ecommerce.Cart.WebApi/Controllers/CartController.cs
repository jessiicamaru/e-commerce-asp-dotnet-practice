using Ecommerce.Cart.Application.Carts;
using Ecommerce.Cart.Application.Carts.Commands.AddToCart;
using Ecommerce.Cart.Application.Carts.Commands.EmptyCart;
using Ecommerce.Cart.Application.Carts.Commands.RemoveLine;
using Ecommerce.Cart.Application.Carts.Commands.SetQuantity;
using Ecommerce.Cart.Application.Carts.Queries.GetMyCart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Cart.WebApi.Controllers;

/// <summary>
/// The caller's own cart. Always the caller's - no route and no body names a user.
/// </summary>
[Authorize]
public class CartController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await Mediator.Send(new GetMyCartQuery()));

    /// <param name="VariantId">
    /// Which shape of the product (specs/020). Omitted means "the product's only variant" - what a
    /// client built before variants sends.
    /// </param>
    public record AddItemRequest(Guid ProductId, int Quantity, Guid? VariantId = null);

    [HttpPost("items")]
    public async Task<IActionResult> Add([FromBody] AddItemRequest request)
    {
        await Mediator.Send(new AddToCartCommand(request.ProductId, request.Quantity, request.VariantId));
        return NoContent();
    }

    public record SetQuantityRequest(int Quantity);

    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> SetQuantity(Guid productId, [FromBody] SetQuantityRequest request)
    {
        await Mediator.Send(new SetQuantityCommand(productId, request.Quantity));
        return NoContent();
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> Remove(Guid productId)
    {
        await Mediator.Send(new RemoveLineCommand(productId));
        return NoContent();
    }

    // ---- A cart before signing in (specs/162, #370): the browser keeps the lines.

    public record CartLinesRequest(List<CartLineInput>? Lines);

    /// <summary>Prices the browser's lines exactly as a stored cart is priced, and stores nothing.</summary>
    [AllowAnonymous]
    [HttpPost("price")]
    public async Task<IActionResult> Price([FromBody] CartLinesRequest request)
        => Ok(await Mediator.Send(new PriceCartLinesQuery(request.Lines ?? [])));

    /// <summary>Merges the browser's lines into the caller's cart - the larger quantity per shape, so a repeat is harmless.</summary>
    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] CartLinesRequest request)
    {
        await Mediator.Send(new MergeCartCommand(request.Lines ?? []));
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Empty()
    {
        await Mediator.Send(new EmptyCartCommand());
        return NoContent();
    }
}
