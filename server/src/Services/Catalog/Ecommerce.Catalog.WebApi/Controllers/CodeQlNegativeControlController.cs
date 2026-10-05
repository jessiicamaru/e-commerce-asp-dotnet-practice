using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.WebApi.Controllers;

// TEMPORARY - specs/150's negative control for CodeQL: a deliberate SQL injection. Reverted before merge.
public class CodeQlNegativeControlController(CatalogDbContext db) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string name)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM products WHERE \"Name\" = '" + name + "'";
        return Ok(await command.ExecuteScalarAsync());
    }
}
