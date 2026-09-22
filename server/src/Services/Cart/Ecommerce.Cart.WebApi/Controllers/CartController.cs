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

    [HttpDelete]
    public async Task<IActionResult> Empty()
    {
        await Mediator.Send(new EmptyCartCommand());
        return NoContent();
    }
}
