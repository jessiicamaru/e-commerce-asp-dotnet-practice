# JWT Security & Token Storage Best Practices

This guide analyzes the security implications of returning tokens directly in the API response body and outlines the industry-standard **Best Practice** approach for Single Page Applications (SPAs).

---

## 1. Security Analysis: Storage Mechanisms

When tokens are returned directly in the JSON response body, the frontend application is responsible for storing them. Usually, developers store them in `localStorage` or `sessionStorage`. 

Below is a comparison of storage options and their vulnerabilities:

| Storage Type | Read access by JavaScript | Vulnerable to XSS? | Vulnerable to CSRF? | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Local Storage / Session Storage** | **Yes** (any JS script on the page can read it) | **High** (If a hacker injects a script via XSS, they steal the token) | **No** (Must be sent manually in headers) | Non-sensitive data, user preferences |
| **In-Memory (JS Variables / State)** | **Yes** (but transient, lost on page refresh) | **Medium** (Harder to scrape, but still extractable) | **No** | Short-lived Access Tokens |
| **HttpOnly, Secure Cookies** | **No** (JS cannot read or access this cookie) | **No** (XSS scripts cannot steal the token) | **Yes** (Mitigated via `SameSite` & anti-forgery tokens) | Sensitive data, **Refresh Tokens** |

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
does five things:

1. **Finds the token.** An unknown token is a 401.
2. **A ROTATED token coming back is reuse.** Two parties hold the same token, and the server cannot
   tell which is the owner, so it revokes **every** active refresh token of that user (every device)
   and answers 401. This is the trade-off the OAuth 2.0 Security BCP recommends. A warning is logged
   with the user id, the token itself is never logged, and since specs/058 a `SessionReuseDetected`
   audit entry records it under Security. A token revoked **without** rotation (by a lock, a ban or an
   earlier reuse sweep; its `ReplacedByToken` is null) is a stale tab, not theft: the same 401, logged
   at Information, and nothing else ends.
3. **Except within 10 seconds of the rotation** (`ReuseGrace`). Two tabs share one HttpOnly cookie,
   so both can send the same token at once. The second gets an ordinary 401 and nothing else is
   revoked.
4. **Asks the account, not the token.** An expired token, or any token of a **locked or banned**
   account (specs/043), is a 401 with the same message. The lock or ban already revoked every session
   when it happened; this holds even for one that slipped through.
5. **Rotates atomically.** In one transaction, a guarded
   `UPDATE ... SET RevokedAt, ReplacedByToken WHERE Token = @t AND RevokedAt IS NULL AND ExpiresAt > now`
   decides the single winner, the successor is inserted, and the user's expired tokens are deleted.
   Ten simultaneous refreshes with one token mint exactly one successor. A revoked token that has not
   expired is **kept**, because it is what recognises a replay.

A successful refresh returns a new access token built from the account as it is **now** - its
current roles included - so a granted or revoked role, or an approved shop, arrives at the next
refresh.

The rows answer the questions afterwards: `RevokedAt` says when a token stopped working, and
`ReplacedByToken` says what replaced it (null when it was revoked by a reuse or by a lock or ban). `RefreshTokenReuseTests`
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
            throw new UnauthorizedAccessException("No session. Sign in again.");   // -> 401
        }

        // 2. Exchange it for a new access token + refresh token. No catch-all (#28): a bad session is
        //    a 401 from the handler; anything else goes through the shared ProblemDetails handler.
        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken));

        // 3. Set the new Refresh Token in the secure cookie
        SetRefreshTokenCookie(result.RefreshToken);

        // 4. Return only the new access token with refresh token hidden
        return Ok(result with { RefreshToken = "" });
    }
```

`register-seller` sets the cookie the same way as `register` and `login`.

---

### 4.4 Signing out (feature 015)

`POST /api/auth/logout` deletes the refresh token and clears the cookie. It has to be the server: the
cookie is HttpOnly, so no script can delete it, and a client that only forgets its access token is
signed straight back in by the next silent refresh. Anonymous and always 204, so an expired access
token never stops someone signing out. The access token already issued keeps working until it
expires - stateless JWT; revoking it early would need a denylist.

The storefront (`client/`) is the first consumer of this whole flow: access token in memory only,
restored on reload through the cookie, one shared refresh for concurrent 401s. It also renews the
session on purpose (`refreshSession` on `AuthState`) when it knows the roles have changed - after a
shop application is approved, before sending the person to `/shop`.

### 4.5 Stopping an account: locks and bans (specs/043)

Staff can stop an account. A **lock** has an end date and a reason and may be set by a moderator (at
most 30 days) or an administrator (up to 365); a **ban** has a reason and no end date, and only an
administrator sets or lifts it. They are two pairs of nullable columns on `users` - `LockedUntil` /
`LockReason` and `BannedAt` / `BanReason` - so they can overlap and an older image still reads the row.

| Moment | What happens |
| :--- | :--- |
| Staff lock or ban | The columns are set and an audit entry is saved; then `RevokeAllRefreshTokensAsync` sets `RevokedAt` on every active refresh token of that user. |
| Sign-in, wrong password or unknown email | **401** `Invalid email or password.` - identical for a stopped account, so the answer reveals nothing. |
| Sign-in, right password, stopped account | **403** through `ForbiddenException`, with a sentence the person can read: `This account is locked until yyyy-MM-dd HH:mm UTC: <reason>` or `This account is banned: <reason>`. Recorded as `SignInRefused`. |
| Refresh | **401** `The session is not valid. Sign in again.` - the account row decides, whatever the token. |
| Requests with an access token already issued | Still accepted until it expires, at most 15 minutes ([#112](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/112)). |
| Unlock or lift | The columns are cleared; the person signs in again. |

The order of the two sign-in checks is the point: before the password is verified, a locked account
and a wrong password must look the same (the #28 rule); after it, the person has proved who they are
and is owed the reason.

A refresh token revoked by a lock has no `ReplacedByToken`. Until specs/058 the handler took it for
**reuse** when the stopped person's browser presented it again (§4.2): it logged a reuse warning and
revoked every session. Once the account had been unlocked, that included the session the person had just
signed in with. That was more than a misleading warning (#128). Now only a rotated token is reuse.
`RefreshTokenReuseTests.A_stale_tab_from_before_a_lock_does_not_end_the_session_after_the_unlock`
holds it.

## 5. Known weaknesses in the current implementation

§4 describes what runs today. Open weaknesses:

1. **An access token outlives a lock or ban** by up to 15 minutes -
   [#112](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/112).
2. **Sign-in can be guessed at without any limit** - no rate limiting at the gateway or in Identity -
   [#105](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/105).
3. **An email address is never confirmed** to belong to whoever registered it -
   [#106](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/106).
4. **A forgotten password cannot be reset, and nobody can change their password or name** -
   [#103](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/103),
   [#104](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/104).

Three earlier weaknesses were recorded here and are fixed:

1. ~~**Every exception becomes "logged out".**~~ **Fixed in #28.** `Refresh()` used to catch
   `Exception` and return `Unauthorized(ex.Message)`, so a database outage looked like an expired
   session and the exception text reached the client. The catch-all is gone; a bad session is a 401
   from the handler, through the shared ProblemDetails handler.
2. ~~**The handler throws bare `Exception`s.**~~ **Fixed in #28** — `UnauthorizedAccessException`.
3. ~~**Rotation deletes the old token instead of revoking it.**~~ **Fixed in #29.** A stolen token
   replayed after its owner rotated it used to be simply "not found". It is now recognised as reuse,
   and every session of that user is revoked (§4.2).
