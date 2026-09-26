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
    /// <summary>A product's questions, newest first. A hidden answer reads as none; no reasons.</summary>
    [AllowAnonymous]
    [HttpGet("products/{productId:guid}/questions")]
    public async Task<IActionResult> ForProduct(Guid productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetProductQuestionsQuery(productId, pageNumber, pageSize)));
    }

    /// <summary>Ask about a product on sale. Its seller is told; asking about your own is 403.</summary>
    [Authorize(Roles = "Customer")]
    [HttpPost("products/{productId:guid}/questions")]
    public async Task<IActionResult> Ask(Guid productId, [FromBody] AskBody body)
    {
        return Ok(await Mediator.Send(new AskQuestionCommand(productId, body.Body)));
    }

    /// <summary>
    /// Answer, or rewrite the answer. Only the product's seller - staff for the shop's own; anybody else is 404, a
    /// hidden question or answer 409.
    /// </summary>
    /// <remarks>
    /// ⚠️ The attribute lets a seller and staff in; WHICH question they may answer depends on the product's row and is
    /// the handler's to decide (specs/027: leaving only Admin here made the ownership check unreachable).
    /// </remarks>
    [Authorize(Roles = "Seller,Admin,Moderator")]
    [HttpPut("questions/{id:guid}/answer")]
    public async Task<IActionResult> Answer(Guid id, [FromBody] AnswerBody body)
    {
        return Ok(await Mediator.Send(new AnswerQuestionCommand(id, body.Answer)));
    }

    /// <summary>What the caller answers for: a seller's own products, or for staff the shop's own.</summary>
    [Authorize(Roles = "Seller,Admin,Moderator")]
    [HttpGet("questions/to-answer")]
    public async Task<IActionResult> ToAnswer([FromQuery] bool answered = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetQuestionsToAnswerQuery(answered, pageNumber, pageSize)));
    }

    /// <summary>Every question, visible or with something hidden, with the reasons.</summary>
    [Authorize(Roles = StaffRoles.Staff)]
    [HttpGet("questions")]
    public async Task<IActionResult> ForStaff([FromQuery] bool hidden = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetQuestionsForStaffQuery(hidden, pageNumber, pageSize)));
    }

    /// <summary>Hide a question and its answer from the product page. The asker is told why.</summary>
    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id, [FromBody] ReasonBody body)
    {
        return Ok(await Mediator.Send(new HideQuestionCommand(id, body.Reason)));
    }

    /// <summary>Show a hidden question again.</summary>
    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        return Ok(await Mediator.Send(new RestoreQuestionCommand(id)));
    }

    /// <summary>Hide only the answer: the question reads as unanswered, and the answer is locked until restored.</summary>
    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("questions/{id:guid}/answer/hide")]
    public async Task<IActionResult> HideAnswer(Guid id, [FromBody] ReasonBody body)
    {
        return Ok(await Mediator.Send(new HideAnswerCommand(id, body.Reason)));
    }

    /// <summary>Show a hidden answer again.</summary>
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
