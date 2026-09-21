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
In [`AuthController.cs`](../../../server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs), we intercept the Command result, extract the Refresh Token, set it as a cookie, and return only the `AuthResponse` details with `RefreshToken` cleared.

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

### 4.2 The handler: rotate, and recognise reuse (#29)

[`RefreshTokenCommandHandler`](../../../server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Refresh/RefreshTokenCommandHandler.cs)
does four things:

1. **Finds the token.** An unknown token is a 401.
2. **A revoked token coming back is reuse.** Two parties hold the same token, and the server cannot
   tell which is the owner, so it revokes **every** active refresh token of that user (every device)
   and answers 401. This is the trade-off the OAuth 2.0 Security BCP recommends. A warning is logged
   with the user id; the token itself is never logged.
3. **Except within 10 seconds of the rotation** (`ReuseGrace`). Two tabs share one HttpOnly cookie,
   so both can send the same token at once. The second gets an ordinary 401 and nothing else is
   revoked.
4. **Rotates atomically.** In one transaction, a guarded
   `UPDATE ... SET RevokedAt, ReplacedByToken WHERE Token = @t AND RevokedAt IS NULL AND ExpiresAt > now`
   decides the single winner, the successor is inserted, and the user's expired tokens are deleted.
   Ten simultaneous refreshes with one token mint exactly one successor. A revoked token that has not
   expired is **kept**, because it is what recognises a replay.

The rows answer the questions afterwards: `RevokedAt` says when a token stopped working, and
`ReplacedByToken` says what replaced it (null when it was revoked by a reuse). `RefreshTokenReuseTests`
covers each case against a real PostgreSQL.

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

---

### 4.4 Signing out (feature 015)

`POST /api/auth/logout` deletes the refresh token and clears the cookie. It has to be the server: the
cookie is HttpOnly, so no script can delete it, and a client that only forgets its access token is
signed straight back in by the next silent refresh. Anonymous and always 204, so an expired access
token never stops someone signing out. The access token already issued keeps working until it
expires - stateless JWT; revoking it early would need a denylist.

The storefront (`client/`) is the first consumer of this whole flow: access token in memory only,
restored on reload through the cookie, one shared refresh for concurrent 401s.

## 5. Known weaknesses in the current implementation

§4 describes what runs today. Three weaknesses were recorded here; all three are now fixed:

1. ~~**Every exception becomes "logged out".**~~ **Fixed in #28.** `Refresh()` used to catch
   `Exception` and return `Unauthorized(ex.Message)`, so a database outage looked like an expired
   session and the exception text reached the client. The catch-all is gone; a bad session is a 401
   from the handler, through the shared ProblemDetails handler.
2. ~~**The handler throws bare `Exception`s.**~~ **Fixed in #28** — `UnauthorizedAccessException`.
3. ~~**Rotation deletes the old token instead of revoking it.**~~ **Fixed in #29.** A stolen token
   replayed after its owner rotated it used to be simply "not found". It is now recognised as reuse,
   and every session of that user is revoked (§4.2).
