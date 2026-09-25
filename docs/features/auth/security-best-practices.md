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

### 4.6 A forgotten password (specs/061)

Two anonymous endpoints, and the rules that make them safe:

1. **`POST /api/auth/forgot-password` `{ email }` is always 202.** An unknown address completes the
   same way as a known one, so the answer cannot be used to learn which emails have accounts (#28).
2. **The link carries a random token; the database keeps its hash.** 32 random bytes, base64url in the
   link; `password_reset_tokens.token_hash` is its SHA-256. Somebody who reads the table cannot use a
   link from it. The link works for **30 minutes**, **once**, and asking again replaces it.
3. **The token never crosses the broker.** Identity is the sender of every email, so it writes the reset
   email straight into its own `outgoing_emails` in the transaction that stores the hash, and the
   dispatcher scrubs the row's data once the email is sent ([email](../email.md)).
4. **`POST /api/auth/reset-password` `{ token, password }` decides in one statement.** A guarded
   `UPDATE ... SET "UsedAt" = now WHERE "TokenHash" = @h AND "UsedAt" IS NULL AND "ExpiresAt" > now
   RETURNING "UserId"`. Of two submissions of one link at once, exactly one resets the password. Used,
   expired, replaced and made-up tokens are the **same 400** on `Token`.
5. **A reset ends every session.** In the same transaction as the new password, every refresh token of
   the account is revoked: whoever held a session - perhaps the reason for the reset - holds it no
   longer. An access token already issued lives out its minutes, as after a lock (#112).
6. **Registration's password rules apply** (at least 8 characters, at most 72 bytes), so a reset is not
   the way round them. Both steps are Security audit entries (`PasswordResetRequested`,
   `PasswordReset`), with neither the token nor the password in them.

`PasswordResetTests` covers each rule against a real PostgreSQL. The storefront's pages are
`/forgot-password` (linked from sign-in) and `/reset-password?token=…`.

### 4.7 Limits on guessing and on email (specs/062)

Until #105 twelve wrong passwords in a row were all answered at once, and since #103 each
`forgot-password` call could send a real email. There are now three limits, each answering **429** as
ProblemDetails, with `Retry-After` in seconds and the same number as `retryAfter` in the body:

1. **Per client IP, at the gateway.** ASP.NET Core's rate limiter uses fixed windows, attached to routes
   through YARP's `RateLimiterPolicy`. Nothing else behind the gateway is limited, and each policy is its
   own allowance:

   | Policy | Routes | Default |
   | :-- | :-- | :-- |
   | `sign-in` | `login`, `register`, `register-seller`, `reset-password` | 30 a minute |
   | `email` | `forgot-password` | 5 a minute |
   | `session` | `refresh` (two tabs share one cookie) | 60 a minute |

   Each is `RateLimits:<policy>:PermitLimit` / `WindowSeconds`, and a value below 1 refuses to start.
   The counters are in memory, per gateway instance.
2. **Per email, in Identity.** After **5** wrong passwords for one email within **15 minutes**, every
   sign-in for that email waits **5 minutes**, the right password included. Otherwise the pause would
   still tell a guesser "right" or "wrong".
   - **Key.** The count is keyed on the email (`EmailKey.For`), not on an account, so an unknown
     address is answered exactly like a real one (#28).
   - **Storage.** It lives in `sign_in_throttles`, changed only by single `INSERT ... ON CONFLICT DO
     UPDATE` statements, so simultaneous wrong passwords are all counted and several instances agree.
   - **Clearing.** A pause puts the count back to 0. The right password or a password reset clears it.
   - **Audit.** Starting a pause for a real account is a Security audit entry, `SignInThrottled`.
   - **Purge.** `SignInThrottleSweeper` deletes stale rows hourly.
   - Settings: `SignIn:MaxFailures`, `WindowMinutes`, `CooldownMinutes`.
3. **Per address, for reset links.** `forgot-password` sends at most **one email a minute** per address,
   however many IPs ask. It locks the person's row, so two requests at once send one email, and the
   answer is still the same 202.

**A pause, not a lock.** The moderation lock (§4.5) would let anybody shut anybody out. The price of a
pause is recorded below.

**The client's IP comes from the connection.** `X-Forwarded-For` is believed only from the proxies in
`GATEWAY_TRUSTED_PROXIES` (IPs or CIDRs), and only its last hop (`ForwardLimit = 1`). In compose that is
the storefront's nginx, at a fixed address on the `edge` network (`172.30.10.10`). Without it every
browser would share nginx's address, and one person's attempts would use up everybody's. ⚠️ With **no**
trusted proxy the header is not read at all (`ForwardedHeaders.None`). Leaving `KnownProxies` and
`KnownIPNetworks` both empty does **not** mean "trust nobody": the middleware then trusts every peer, and
a client that wrote a new address on each request was never limited. `AuthRateLimitTests` found that.

`SignInThrottleTests` (Identity, real PostgreSQL) and `AuthRateLimitTests` (the gateway's real pipeline,
new in `Ecommerce.ApiGateway.Tests`) cover each rule. The storefront's sign-in, sign-up, forgot-password
and reset-password pages say "Too many attempts. Try again in N minutes." in the reader's language.

### 4.8 Confirming an email address (specs/063)

Until #106 anybody could register with somebody else's address and be treated as its owner: they could
open a shop in that name, and would receive that person's order confirmations and reset links.

1. **Registering sends a link.** Both registrations stage a single-use token and an `EmailConfirmation`
   email in the account's own save, so there is no account without its link. Only the token's SHA-256
   hash is stored (`email_confirmation_tokens`), and the sent row is scrubbed, as with a reset link (§4.6).
   The link works for **24 hours**: a confirmation grants nothing an attacker wants, and people open
   welcome emails late.
2. **`POST /api/auth/confirm-email` `{ token }` is anonymous**, because the link may be opened in another
   browser. It runs one guarded claim on the token and one guarded
   `UPDATE users ... WHERE "EmailConfirmedAt" IS NULL`, in one transaction. A used, expired, replaced or
   made-up token is one 400, and two submissions at once confirm once.
3. **`POST /api/auth/resend-confirmation` is signed in**: the account comes from the token. It answers
   202, sends at most one email a minute under the person's row lock, and is limited per IP at the
   gateway (`email`). An address already confirmed gets 409.
4. **What waits for it: selling, not buying.** An unconfirmed customer who applies to sell gets 403
   `EmailNotConfirmed`. Registering as a seller still creates the application, but **approving** it while
   the address is unconfirmed is 409, and staff see the flag on the application. Browsing, the cart and
   checkout stay open.
5. **Accounts from before this count as confirmed.** The migration sets `EmailConfirmedAt = CreatedAt`,
   and the administrator seeded at startup is confirmed. Asking again would stop shops that already
   trade.
6. `emailConfirmed` is on every authentication response, for drawing the storefront's banner (like
   `roles`, never for deciding). Both steps are User audit entries: `EmailConfirmationSent` and
   `EmailConfirmed`.

`EmailConfirmationTests` covers each rule against a real PostgreSQL.

## 5. Known weaknesses in the current implementation

§4 describes what runs today. Open weaknesses:

1. **An access token outlives a lock or ban** by up to 15 minutes -
   [#112](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/112).
2. **Somebody who knows an email can keep its sign-in paused.** 5 wrong passwords every 5 minutes
   do it. They cannot get in, and the per-IP limit caps how many addresses one client can do this to.
   This is the price of a pause per email (§4.7); a pause per email *and* IP would let a guesser with
   many addresses go unslowed.
3. **The gateway's counters are per instance.** Several gateways would each allow the full rate.
4. **Nobody can change their password or name while signed in** -
   [#104](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/104). A forgotten
   password can be reset since specs/061 (§4.6).

Five earlier weaknesses were recorded here and are fixed:

1. ~~**Every exception becomes "logged out".**~~ **Fixed in #28.** `Refresh()` used to catch
   `Exception` and return `Unauthorized(ex.Message)`, so a database outage looked like an expired
   session and the exception text reached the client. The catch-all is gone; a bad session is a 401
   from the handler, through the shared ProblemDetails handler.
2. ~~**The handler throws bare `Exception`s.**~~ **Fixed in #28** — `UnauthorizedAccessException`.
3. ~~**Rotation deletes the old token instead of revoking it.**~~ **Fixed in #29.** A stolen token
   replayed after its owner rotated it used to be simply "not found". It is now recognised as reuse,
   and every session of that user is revoked (§4.2).
4. ~~**Sign-in can be guessed at without any limit.**~~ **Fixed in specs/062 (#105)** - three limits
   (§4.7).
5. ~~**An email address is never confirmed.**~~ **Fixed in specs/063 (#106)** - a link on registering, and
   no shop until it is used (§4.8).
