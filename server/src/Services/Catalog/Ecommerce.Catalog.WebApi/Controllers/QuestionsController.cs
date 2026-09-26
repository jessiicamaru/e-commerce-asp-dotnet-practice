using Ecommerce.Catalog.Application.Questions;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>
/// Questions about a product (specs/076). Reading a product's is public; asking is a customer's; answering is the
/// product's seller's - or staff's, for the shop's own - and anybody else is a 404; hiding and restoring are Staff.
/// </summary>
[Route("api")]
public class QuestionsController : ApiControllerBase
{
    private const string Answerers = "Seller," + StaffRoles.Staff;

    [AllowAnonymous]
    [HttpGet("products/{productId:guid}/questions")]
    public async Task<IActionResult> ForProduct(Guid productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetProductQuestionsQuery(productId, pageNumber, pageSize)));
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("products/{productId:guid}/questions")]
    public async Task<IActionResult> Ask(Guid productId, [FromBody] AskBody body)
    {
        return Ok(await Mediator.Send(new AskQuestionCommand(productId, body.Body)));
    }

    /// <summary>
    /// ⚠️ The attribute lets a seller and staff in; WHICH question they may answer depends on the product's row and is
    /// the handler's to decide (specs/027: leaving only Admin here made the ownership check unreachable).
    /// </summary>
    [Authorize(Roles = Answerers)]
    [HttpPut("questions/{id:guid}/answer")]
    public async Task<IActionResult> Answer(Guid id, [FromBody] AnswerBody body)
    {
        return Ok(await Mediator.Send(new AnswerQuestionCommand(id, body.Answer)));
    }

    [Authorize(Roles = Answerers)]
    [HttpGet("questions/to-answer")]
    public async Task<IActionResult> ToAnswer([FromQuery] bool answered = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetQuestionsToAnswerQuery(answered, pageNumber, pageSize)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpGet("questions")]
    public async Task<IActionResult> ForStaff([FromQuery] bool hidden = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetQuestionsForStaffQuery(hidden, pageNumber, pageSize)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id, [FromBody] ReasonBody body)
    {
        return Ok(await Mediator.Send(new HideQuestionCommand(id, body.Reason)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        return Ok(await Mediator.Send(new RestoreQuestionCommand(id)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/answer/hide")]
    public async Task<IActionResult> HideAnswer(Guid id, [FromBody] ReasonBody body)
    {
        return Ok(await Mediator.Send(new HideAnswerCommand(id, body.Reason)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/answer/restore")]
    public async Task<IActionResult> RestoreAnswer(Guid id)
    {
        return Ok(await Mediator.Send(new RestoreAnswerCommand(id)));
    }

    public record AskBody(string Body);

    public record AnswerBody(string Answer);

    public record ReasonBody(string Reason);
}
