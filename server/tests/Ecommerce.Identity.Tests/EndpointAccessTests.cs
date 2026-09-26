using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Every action of this service says who may call it - [Authorize] or [AllowAnonymous] - rather than being public
/// because nothing said otherwise (#183, specs/089). Needs no database.
/// </summary>
/// <remarks>
/// The second test holds <see cref="EndpointAccess.Undeclared"/> itself to finding what it should, so the one-line
/// test in every service cannot pass because the helper finds nothing. It lives here beside
/// <see cref="FallbackPolicyTests"/>: Ecommerce.Shared has no test project of its own.
/// </remarks>
public class EndpointAccessTests
{
    [Fact]
    public void Every_action_says_who_may_call_it() =>
        Assert.Empty(EndpointAccess.Undeclared(typeof(Ecommerce.WebApi.Controllers.AuthController).Assembly));

    [Fact]
    public void An_action_that_says_nothing_is_named_and_one_that_says_either_is_not() =>
        Assert.Equal(["SaysNothingController.Get"], EndpointAccess.Undeclared(typeof(SaysNothingController).Assembly));
}

public class SaysNothingController : ControllerBase
{
    public IActionResult Get() => Ok();

    [Authorize]
    public IActionResult Private() => Ok();

    [AllowAnonymous]
    public IActionResult Public() => Ok();

    [NonAction]
    public void Helper() { }
}

[Authorize]
public class SignedInController : ControllerBase
{
    public IActionResult Get() => Ok();

    [AllowAnonymous]
    public IActionResult Public() => Ok();
}
