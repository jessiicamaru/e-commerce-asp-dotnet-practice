using Ecommerce.Activity.Application.MyData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Activity.WebApi.Controllers;

/// <summary>Everything Activity holds about the caller, and what it withholds and why (#217, specs/111).</summary>
[Route("api/notifications")]
[Authorize]
public class MyDataController : ApiControllerBase
{
    [HttpGet("my-data")]
    public async Task<IActionResult> Get() => Ok(await Mediator.Send(new GetMyDataQuery()));
}
