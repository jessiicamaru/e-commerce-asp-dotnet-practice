using Ecommerce.Shared.Authentication;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// Every action of this service says who may call it - [Authorize] or [AllowAnonymous] - rather than being public
/// because nothing said otherwise (#183, specs/089). Needs no database.
/// </summary>
public class EndpointAccessTests
{
    [Fact]
    public void Every_action_says_who_may_call_it() =>
        Assert.Empty(EndpointAccess.Undeclared(typeof(Ecommerce.Inventory.WebApi.Controllers.StockController).Assembly));
}
