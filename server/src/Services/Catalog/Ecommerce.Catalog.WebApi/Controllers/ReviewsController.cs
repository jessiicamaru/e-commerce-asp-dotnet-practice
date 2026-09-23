using Ecommerce.Catalog.Application.Reviews;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>
/// Reviews (specs/046). Reading a product's is public; writing is the caller's own, one per product, and
/// only after receiving it; hiding and restoring are Staff.
/// </summary>
[Route("api")]
public class ReviewsController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("products/{productId:guid}/reviews")]
    public async Task<IActionResult> ForProduct(Guid productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetProductReviewsQuery(productId, pageNumber, pageSize)));
    }

    [Authorize]
    [HttpGet("products/{productId:guid}/reviews/mine")]
    public async Task<IActionResult> Mine(Guid productId)
    {
        return Ok(await Mediator.Send(new GetMyReviewQuery(productId)));
    }

    [Authorize(Roles = "Customer")]
    [HttpPut("products/{productId:guid}/reviews/mine")]
    public async Task<IActionResult> Write(Guid productId, [FromBody] ReviewBody body)
    {
        return Ok(await Mediator.Send(new WriteReviewCommand(productId, body.Rating, body.Body)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpGet("reviews")]
    public async Task<IActionResult> ForStaff([FromQuery] bool hidden = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetReviewsForStaffQuery(hidden, pageNumber, pageSize)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("reviews/{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id, [FromBody] HideBody body)
    {
        return Ok(await Mediator.Send(new HideReviewCommand(id, body.Reason)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("reviews/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        return Ok(await Mediator.Send(new RestoreReviewCommand(id)));
    }

    public record ReviewBody(int Rating, string? Body);

    public record HideBody(string Reason);
}
