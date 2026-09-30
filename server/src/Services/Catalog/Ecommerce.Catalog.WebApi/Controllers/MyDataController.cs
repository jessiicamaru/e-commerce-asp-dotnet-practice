using Ecommerce.Catalog.Application.MyData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>Everything Catalog holds about the caller, and what it withholds and why (#217, specs/111).</summary>
[Route("api/products")]
[Authorize]
public class MyDataController : ApiControllerBase
{
    [HttpGet("my-data")]
    public async Task<IActionResult> Get() => Ok(await Mediator.Send(new GetMyDataQuery()));
}
