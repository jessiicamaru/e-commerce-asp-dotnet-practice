using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

public class ProductsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
