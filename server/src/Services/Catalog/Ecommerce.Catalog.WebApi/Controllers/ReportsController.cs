using Ecommerce.Catalog.Application.Reports;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>
/// Shoppers report a review, a question or a product; staff work through what was reported (#199, specs/101). Acting on
/// a report is the existing hide or take-down, which closes the reports itself; dismissing is here.
/// </summary>
public class ReportsController : ApiControllerBase
{
    /// <summary>Anybody signed in. Who reports comes from the token.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Report([FromBody] ReportContentCommand command) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command));

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpGet]
    public async Task<IActionResult> Queue([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12) =>
        Ok(await Mediator.Send(new GetReportQueueQuery(pageNumber, pageSize)));

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("{targetType}/{targetId:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(string targetType, Guid targetId)
    {
        await Mediator.Send(new DismissReportsCommand(targetType, targetId));
        return NoContent();
    }
}
