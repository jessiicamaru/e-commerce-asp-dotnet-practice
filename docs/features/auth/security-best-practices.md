# JWT Security & Token Storage Best Practices

This guide analyzes the security implications of returning tokens directly in the API response body and outlines the industry-standard **Best Practice** approach for Single Page Applications (SPAs).

---

## 1. Security Analysis: Storage Mechanisms

When tokens are returned directly in the JSON response body, the frontend application is responsible for storing them. Usually, developers store them in `localStorage` or `sessionStorage`. 

Below is a comparison of storage options and their vulnerabilities:

| Storage Type | Read access by JavaScript | Vulnerable to XSS? | Vulnerable to CSRF? | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Local Storage / Session Storage** | **Yes** (any JS script on the page can read it) | 🔴 **High** (If a hacker injects a script via XSS, they steal the token) | 🟢 **No** (Must be sent manually in headers) | Non-sensitive data, user preferences |
| **In-Memory (JS Variables / State)** | **Yes** (but transient, lost on page refresh) | 🟡 **Medium** (Harder to scrape, but still extractable) | 🟢 **No** | Short-lived Access Tokens |
| **HttpOnly, Secure Cookies** | 🚫 **No** (JS cannot read or access this cookie) | 🟢 **No** (XSS scripts cannot steal the token) | 🔴 **Yes** (Mitigated via `SameSite` & anti-forgery tokens) | Sensitive data, **Refresh Tokens** |

---

## 2. The Recommended Approach (Best Practice)

For a secure balance between convenience and high security, follow this hybrid architecture:

```
┌────────────────┐                     ┌────────────────┐
│   SPA Client   │                     │  ASP.NET Core  │
│  (React/Vue)   │                     │    Web API     │
└───────┬────────┘                     └───────┬────────┘
        │                                      │
        │  1. POST /api/auth/login             │
        ├─────────────────────────────────────►│
        │                                      │ 2. Generate Access Token &
        │                                      │    Refresh Token
        │                                      │
        │  3. Response:                        │
        │     - JSON Body: AccessToken         │
        │     - Cookie (HttpOnly):RefreshToken │
        │◄─────────────────────────────────────┤
        │                                      │
```

1. **Access Token (Short-lived, e.g., 15 minutes)**:
   - Returned in the **JSON Response Body**.
   - Stored in **In-Memory** state (e.g., React Context, Pinia, Redux) by the frontend.
   - Attached to the `Authorization: Bearer <token>` header for all API requests.
2. **Refresh Token (Long-lived, e.g., 7 days)**:
   - **Never** returned in the response body.
   - Sent by the backend as an **HttpOnly, Secure, SameSite=Lax Cookie**.
   - Used only to request a new Access Token when the current one expires via `/api/auth/refresh`.

### Why this is secure:
* If an XSS vulnerability occurs on the client, the attacker **cannot** read the Refresh Token because JavaScript has no access to `HttpOnly` cookies.
* The Access Token is in-memory, so even if extracted, it expires in 15 minutes.
* CSRF is mitigated on the `/refresh` endpoint by configuring `SameSite=Lax` or `SameSite=Strict` and enforcing matching origins.

---

## 3. How to Implement in ASP.NET Core

### 3.1 Step 1: Modifying AuthResponse
We keep `AuthResponse` containing both tokens in the Application layer, but we filter out `RefreshToken` at the Controller layer.

### 3.2 Step 2: Setting the HttpOnly Cookie in the Controller
In [`AuthController.cs`](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.WebApi/Controllers/AuthController.cs), we intercept the Command result, extract the Refresh Token, set it as a cookie, and return only the `AuthResponse` details with `RefreshToken` cleared.

```csharp
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Register;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

public class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await Mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);

        // Hide refresh token from HTTP response body using C# "with" expression
        return Ok(result with { RefreshToken = "" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);
        
        return Ok(result with { RefreshToken = "" });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,         // Prevents JavaScript access (XSS Protection)
            Secure = true,           // Enforces HTTPS only
            SameSite = SameSiteMode.Lax, // Mitigates CSRF attacks
            Expires = DateTime.UtcNow.AddDays(7) // Matches token expiry
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
```

---

## 4. The Token Refresh Flow (Rolling Refresh Token)

To allow the frontend to automatically refresh the `AccessToken` when it expires, you must implement a `/refresh` endpoint.

### 4.1 Define the Refresh Token Command
Create `RefreshTokenCommand.cs` in `Application/Auth/Commands/Refresh/`:

```csharp
using MediatR;
using Ecommerce.Application.Auth.Common;

namespace Ecommerce.Application.Auth.Commands.Refresh;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;
```

### 4.2 Create the Handler
Create `RefreshTokenCommandHandler.cs` in `Application/Auth/Commands/Refresh/`. The handler should:
1. Fetch the user associated with this Refresh Token from the database.
2. Verify if the token is valid, not expired, and not revoked.
3. Generate a new Access Token and a **new Refresh Token** (Rolling Refresh).
4. Revoke/remove the old Refresh Token and append the new one.
5. Save changes to the database and return the DTO.

```csharp
using MediatR;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Domain.Entities;
using Ecommerce.Application.Common.Constants;

namespace Ecommerce.Application.Auth.Commands.Refresh;

public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator
) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch user by refresh token
        var user = await _userRepository.GetByUserRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (user == null)
        {
            throw new Exception("Invalid session.");
        }

        // 2. Locate the active token
        var activeToken = user.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);
        if (activeToken == null || activeToken.IsExpired || activeToken.RevokedAt != null)
        {
            throw new Exception("Session expired or invalid.");
        }

        // 3. Revoke/remove the old refresh token
        user.RefreshTokens.Remove(activeToken);

        // 4. Generate new tokens
        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var newRefreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        // 5. Save the new refresh token
        user.RefreshTokens.Add(new RefreshToken
        {
            Token = newRefreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
        });

        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            newAccessToken,
            newRefreshTokenString
        );
    }
}
```
*(Note: You will need to add the method `GetByUserRefreshTokenAsync(string token, CancellationToken cancellationToken)` to your `IUserRepository` interface and implement it in `UserRepository` using `_context.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == token))`)*

### 4.3 Add the Endpoint to AuthController
Add the refresh endpoint to `AuthController.cs` in the WebApi project:

```csharp
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        // 1. Read the Refresh Token from the secure cookie
        if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized("No session cookie found.");
        }

        try
        {
            // 2. Send the command to exchange it for a new access token + refresh token
            var result = await Mediator.Send(new RefreshTokenCommand(refreshToken));

            // 3. Set the new Refresh Token in the secure cookie
            SetRefreshTokenCookie(result.RefreshToken);

            // 4. Return only the new access token with refresh token hidden
            return Ok(result with { RefreshToken = "" });
        }
        catch (Exception ex)
        {
            return Unauthorized(ex.Message);
        }
    }
```
