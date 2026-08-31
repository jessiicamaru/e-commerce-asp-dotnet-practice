using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);

        SetRefreshTokenCookie(result.RefreshToken);

        // Hide refresh token from HTTP response body
        return Ok(result with { RefreshToken = "" });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized("No session cookie found.");
        }

        try
        {
            var result = await Mediator.Send(new RefreshTokenCommand(refreshToken));

            SetRefreshTokenCookie(result.RefreshToken);

            return Ok(result with { RefreshToken = "" });
        }
        catch (Exception ex)
        {
            return Unauthorized(ex.Message);
        }
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
