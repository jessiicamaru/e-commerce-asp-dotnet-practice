using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Logout;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Commands.RegisterSeller;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Common.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

public class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await Mediator.Send(command);

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
    [HttpPost("register-seller")]
    public async Task<IActionResult> RegisterSeller([FromBody] RegisterSellerCommand command)
    {
        var result = await Mediator.Send(command);

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result with { RefreshToken = "" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);

        SetRefreshTokenCookie(result.RefreshToken);

        // Hide refresh token from HTTP response body
        return Ok(result with { RefreshToken = "" });
    }

    /// <summary>
    /// Asks for a link to choose a new password (specs/061). Always 202, whether or not the address has an
    /// account (#28). The email is written in the language the request comes in.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await Mediator.Send(command with { Language = RequestLanguage() });
        return Accepted();
    }

    /// <summary>Chooses a new password with the link's token (specs/061); every session ends. 204, or 400.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>The first language of <c>Accept-Language</c>, primary tag only ("en-US" is "en"); empty when none.</summary>
    private string RequestLanguage()
    {
        var first = Request.Headers.AcceptLanguage.ToString().Split(',')[0].Split(';')[0].Trim();
        return first.Length >= 2 ? first[..2].ToLowerInvariant() : string.Empty;
    }

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
        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken));

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result with { RefreshToken = "" });
    }

    /// <summary>
    /// Ends the session: the refresh token is deleted server-side and the cookie is cleared. Anonymous
    /// on purpose - an expired access token must not stop someone signing out. Always 204.
    /// </summary>
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
