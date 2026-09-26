using Ecommerce.Application.Auth.Commands.Account;
using Ecommerce.Application.Auth.Commands.EmailConfirmation;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Logout;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Commands.RegisterSeller;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// Signing in and out, registering, and the caller's own account. Signed in by default, and every action that is
/// not says so with [AllowAnonymous] (#183, specs/089): before, these were public only because nothing said
/// otherwise, and the next action added here would have been public for the same reason.
/// </summary>
[Authorize]
public class AuthController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await Mediator.Send(command with { Language = RequestLanguage() });

        SetRefreshTokenCookie(result.RefreshToken);

        // Hide refresh token from HTTP response body
        return Ok(result with { RefreshToken = "" });
    }

    /// <summary>
    /// Registers somebody who sells, with the name their shop trades under (specs/027).
    /// </summary>
    /// <remarks>
    /// A separate endpoint rather than a flag on <c>register</c>: a boolean in a body that changes
    /// what an account <b>is</b> has the shape of the two defects this project has already fixed.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("register-seller")]
    public async Task<IActionResult> RegisterSeller([FromBody] RegisterSellerCommand command)
    {
        var result = await Mediator.Send(command with { Language = RequestLanguage() });

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result with { RefreshToken = "" });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command with { Language = RequestLanguage() });

        SetRefreshTokenCookie(result.RefreshToken);

        // Hide refresh token from HTTP response body
        return Ok(result with { RefreshToken = "" });
    }

    /// <summary>
    /// Asks for a link to choose a new password (specs/061). Always 202, whether or not the address has an
    /// account (#28). The email is written in the language the request comes in.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await Mediator.Send(command with { Language = RequestLanguage() });
        return Accepted();
    }

    /// <summary>Chooses a new password with the link's token (specs/061); every session ends. 204, or 400.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>Uses the link sent to confirm an address (specs/063). Anonymous - it may be opened anywhere. 204, or 400.</summary>
    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Sends the signed-in caller a new confirmation link (specs/063): 202, at most one email a minute; 409 when
    /// the address is already confirmed.
    /// </summary>
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation()
    {
        await Mediator.Send(new ResendConfirmationCommand(RequestLanguage()));
        return Accepted();
    }

    /// <summary>The caller's own details (specs/064) - from the token, never from the request.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me() => Ok(await Mediator.Send(new GetMeQuery()));

    /// <summary>Changes the caller's own name and phone (specs/064). The email is not changed here.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeCommand command) => Ok(await Mediator.Send(command));

    /// <summary>
    /// Changes the caller's own password (specs/064): 204, or 400 when the current one is wrong. Every other
    /// session ends; this browser's - named by its HttpOnly cookie, never by the body - stays.
    /// </summary>
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        Request.Cookies.TryGetValue("refreshToken", out var thisSession);
        await Mediator.Send(command with { KeepRefreshToken = string.IsNullOrEmpty(thisSession) ? null : thisSession });
        return NoContent();
    }

    /// <summary>The first language of <c>Accept-Language</c>, primary tag only ("en-US" is "en"); empty when none.</summary>
    private string RequestLanguage()
    {
        var first = Request.Headers.AcceptLanguage.ToString().Split(',')[0].Split(';')[0].Trim();
        return first.Length >= 2 ? first[..2].ToLowerInvariant() : string.Empty;
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedAccessException("No session. Sign in again.");
        }

        // No catch-all (issue #28). It used to turn EVERY exception - a database outage included - into
        // "logged out", with the exception's own text as the body. A bad session is now a 401 from the
        // handler; anything else is what it is, through the shared ProblemDetails handler.
        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken, RequestLanguage()));

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result with { RefreshToken = "" });
    }

    /// <summary>
    /// Ends the session: the refresh token is deleted server-side and the cookie is cleared. Anonymous
    /// on purpose - an expired access token must not stop someone signing out. Always 204.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        Request.Cookies.TryGetValue("refreshToken", out var refreshToken);

        await Mediator.Send(new LogoutCommand(refreshToken));

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax
        });

        return NoContent();
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Set to true for Production HTTPS, local development supports Secure cookies if HTTPS is used
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
