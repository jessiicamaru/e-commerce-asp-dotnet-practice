using Ecommerce.Application.Email;
using Ecommerce.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// The words of the emails the shop sends (specs/077), edited by an administrator - not a moderator: these reach
/// every customer's inbox, and two of them carry the link that resets a password.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/email-templates")]
public class EmailTemplatesController : ApiControllerBase
{
    /// <summary>Every template in every language, as it currently reads, with the placeholders it may use.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await Mediator.Send(new GetEmailTemplatesQuery()));
    }

    /// <summary>One template's saved versions in one language, newest first.</summary>
    [HttpGet("{template}/{language}/versions")]
    public async Task<IActionResult> Versions(string template, string language)
    {
        return Ok(await Mediator.Send(new GetEmailTemplateVersionsQuery(template, language)));
    }

    /// <summary>Save new words: sanitised, placeholders checked, a new version. A stale `expectedVersion` is 409.</summary>
    [HttpPut("{template}/{language}")]
    public async Task<IActionResult> Save(string template, string language, [FromBody] SaveBody body)
    {
        return Ok(await Mediator.Send(new SaveEmailTemplateCommand(template, language, body.Subject, body.BodyHtml, body.ExpectedVersion)));
    }

    /// <summary>Back to the built-in words, as a new version.</summary>
    [HttpPost("{template}/{language}/reset")]
    public async Task<IActionResult> Reset(string template, string language, [FromBody] VersionBody body)
    {
        return Ok(await Mediator.Send(new ResetEmailTemplateCommand(template, language, body.ExpectedVersion)));
    }

    /// <summary>An earlier version's words, as a new version.</summary>
    [HttpPost("{template}/{language}/versions/{version:int}/restore")]
    public async Task<IActionResult> Restore(string template, string language, int version, [FromBody] VersionBody body)
    {
        return Ok(await Mediator.Send(new RestoreEmailTemplateVersionCommand(template, language, version, body.ExpectedVersion)));
    }

    /// <summary>A draft filled with made-up data - nothing saved, nothing sent.</summary>
    [HttpPost("{template}/{language}/preview")]
    public async Task<IActionResult> Preview(string template, string language, [FromBody] DraftBody body)
    {
        return Ok(await Mediator.Send(new PreviewEmailTemplateCommand(template, language, body.Subject, body.BodyHtml)));
    }

    /// <summary>A draft filled with made-up data, sent to the caller's own address - nothing saved.</summary>
    [HttpPost("{template}/{language}/test")]
    public async Task<IActionResult> Test(string template, string language, [FromBody] DraftBody body)
    {
        return Ok(await Mediator.Send(new SendTestEmailCommand(template, language, body.Subject, body.BodyHtml)));
    }

    public record SaveBody(string Subject, string BodyHtml, int ExpectedVersion);

    public record VersionBody(int ExpectedVersion);

    public record DraftBody(string Subject, string BodyHtml);
}
