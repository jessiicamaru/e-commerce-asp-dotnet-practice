using Ecommerce.Order.Application.MyData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

/// <summary>Everything Order holds about the caller, and what it withholds and why (#217, specs/111).</summary>
[Route("api/orders")]
[Authorize]
public class MyDataController : ApiControllerBase
{
    [HttpGet("my-data")]
    public async Task<IActionResult> Get() => Ok(await Mediator.Send(new GetMyDataQuery()));
}
