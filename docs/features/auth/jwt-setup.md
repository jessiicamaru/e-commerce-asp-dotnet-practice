# JWT Authentication Setup Guide

This guide details the packages, configuration, and setup required to implement JWT (JSON Web Token) authentication in the E-commerce system.

---

## 1. Package Reference & Installation

| Package Name | Target Project | Purpose |
| :--- | :--- | :--- |
| **`BCrypt.Net-Next`** | `Identity.Infrastructure` | Password hashing and verification. |
| **`System.IdentityModel.Tokens.Jwt`** | `Identity.Infrastructure` | Creating and signing access tokens. |
| **`Microsoft.IdentityModel.Tokens`** | `Identity.Infrastructure` | Cryptographic keys and signing credentials. |
| **`Microsoft.AspNetCore.Authentication.JwtBearer`** | `Ecommerce.Shared` | Validating incoming tokens. Lives in the shared building block so every service validates identically. |

Only the Identity service **signs** tokens. Every other service only **validates** them, and gets that
capability by referencing `Ecommerce.Shared` — no per-service JWT packages.

## 2. Configuration Settings

### Step 2.1: Add JWT Options in `appsettings.json`
Add the following `JwtSettings` section to your [`appsettings.json`](../../../server/src/Services/Identity/Ecommerce.Identity.WebApi/appsettings.json):

```json
  "JwtSettings": {
    "Secret": "", // Read from environment variable JWT_SECRET in production/local development
    "Issuer": "EcommerceApi",
    "Audience": "EcommerceClients",
    "ExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
```

---

## 3. Implementation Steps

Token validation is centralized in
[`Ecommerce.Shared/Authentication/`](../../../server/src/BuildingBlocks/Ecommerce.Shared/Authentication/),
so a service does not hand-roll `AddJwtBearer`. It calls one extension method:

```csharp
using Ecommerce.Shared.Authentication;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);   // <-- validates tokens

var app = builder.Build();

// Must be in this exact order, and both before MapControllers().
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

`AddJwtAuthentication` also registers `ICurrentUser`, which exposes the authenticated caller to the
Application layer without dragging `HttpContext` into it:

```csharp
public class SubmitOrderCommandHandler(ICurrentUser currentUser, ...)
{
    public async Task<OrderResponse> Handle(SubmitOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        // ...
    }
}
```

> **Never take the user id from the request body.** A field like `SubmitOrderCommand.UserId` lets any
> caller place an order on behalf of anyone else. It has to come from the validated token.

`ICurrentUser` exposes:

| Member | Read from | Notes |
| :--- | :--- | :--- |
| `Id` | `sub` | `null` when absent or not a GUID. |
| `Email` | `email` | |
| `GivenName` | `given_name` | The name a review is signed with (specs/046). A default interface member returning `null`, so a token issued before the claim existed, and every test double, still works; Catalog then signs with the email's first letter. |
| `IsAuthenticated` | the principal | |
| `IsInRole(role)` | every `role` claim | Reads the short claim name that is actually in the token, not `ClaimsPrincipal.IsInRole`. For checks an attribute cannot make, because they depend on a row: "may this seller write to THIS product", "may this moderator lock THIS account". |

### 3.0 What the access token carries

[`JwtTokenGenerator`](../../../server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Security/JwtTokenGenerator.cs)
signs HMAC-SHA256 tokens that live `ExpiryMinutes` (15):

| Claim | Value |
| :--- | :--- |
| `sub` | The user id. |
| `email` | The email as the person typed it. |
| `given_name` | The first name only - never the surname (specs/046). |
| `jti` | A fresh GUID per token. |
| `role` | One claim per role held: `Admin`, `Customer`, `Seller`, `Moderator`. |
| `exp`, `iss`, `aud` | Expiry, `JwtSettings:Issuer`, `JwtSettings:Audience`. |

The authentication response also returns the same roles as `roles`, so the storefront can decide what
to **draw** without decoding the token. It is not a permission: authorization lives in the controller
attributes and the handlers.

### 3.1 The secret must reach every service

Each service loads `JWT_SECRET` from the environment and writes it into configuration, because the
`.env` loader runs *after* `WebApplication.CreateBuilder`, so the environment-variable configuration
provider has already been built and will not see it:

```csharp
var envJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrEmpty(envJwtSecret))
{
    builder.Configuration["JwtSettings:Secret"] = envJwtSecret;
}
```

Forgetting this in a new service stops it at startup: `JWT_SECRET` is named in the error.

**`AddJwtAuthentication` refuses to start with incomplete settings** (issue #30). An empty
`Issuer`, an empty `Audience`, or a secret shorter than 32 bytes (too short for HMAC-SHA256) throws at
startup, and the error names every problem at once:

```text
Unhandled exception. System.InvalidOperationException: JWT settings are incomplete, so every token
would be rejected: 'JwtSettings:Audience' is empty - add it to this service's appsettings.json.
```

Before this check, an empty `Issuer` or `Audience` started cleanly, reported healthy, and rejected
every token. Cart did exactly that in feature 010, answering `401` with `IDX10208: Unable to validate
audience` because it had no `appsettings.json`. (A missing section and an empty secret already
failed at startup.) A new service still needs its own `JwtSettings` (`Issuer`, `Audience`) **and** the
`JWT_SECRET` copy above; it now finds out at startup, not from its first request.
`JwtStartupTests` in `Ecommerce.Identity.Tests` covers each case.

### 3.2 Three traps worth knowing

| Trap | What happens | How it is handled |
| :--- | :--- | :--- |
| **Inbound claim mapping** | `JwtSecurityTokenHandler` rewrites `sub` to `ClaimTypes.NameIdentifier`, so looking up `"sub"` returns `null`. | `options.MapInboundClaims = false` keeps claim names verbatim. |
| **Role claim name** | The signing side adds `ClaimTypes.Role` (a long URI), but the handler **shortens it to `"role"`** when writing the token. Validating against the long URI rejects every administrator with `403`. | `RoleClaimType = "role"`. |
| **Clock skew** | The default 5-minute tolerance keeps a 15-minute token usable for 20. | `ClockSkew = TimeSpan.Zero`. |

The role trap is easy to miss because a hand-crafted test token signed with your *own* assumptions
will pass. It only shows up against a token the Identity service actually issued — which is why CI
runs [`verify-auth.sh`](../../../.github/scripts/verify-auth.sh) against
real logins.

## 4. Verifying JWT Token in Endpoints
To protect an endpoint, add the `[Authorize]` attribute above your Controller class or actions:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    [Authorize] // Requires a valid JWT token in Authorization header
    public IActionResult GetSecretProducts()
    {
        return Ok("Secret catalog accessible!");
    }
}
```
In requests, clients must pass the token in the `Authorization` header:
`Authorization: Bearer <your_jwt_token>`

### 4.1 Role-based authorization

`[Authorize(Roles = "Admin")]` restricts an endpoint to a role. Roles are seeded on Identity startup
(see [Database Schema Design](./db-design.md)) and travel inside the token. There are four:

| Role | Held by |
| :--- | :--- |
| `Customer` | Everybody who registers. |
| `Seller` | A person whose shop application was approved - always together with `Customer`. |
| `Moderator` | Somebody an administrator made a moderator. The only role the API can grant or revoke. |
| `Admin` | The first administrator, seeded from `ADMIN_EMAIL` / `ADMIN_PASSWORD`. |

Staff endpoints name both staff roles through one constant in `Ecommerce.Shared.Authentication`:

```csharp
[Authorize(Roles = StaffRoles.Staff)]   // "Admin,Moderator" - the comma means any of
```

Who reaches which endpoint (the full, generated list is [api.md](../../reference/api.md)):

| Endpoints | Access |
| :--- | :--- |
| `POST /api/auth/*` (register, register-seller, login, refresh, logout) | Anonymous |
| Catalogue reads - products, categories, images, a product's reviews, `GET /api/stock`, `GET /api/orders/shipping-options`, `POST /api/products/{id}/view` | Anonymous |
| `/api/cart`, `/api/addresses`, `/api/notifications`, `POST /api/orders`, `GET /api/orders/quote`, `GET /api/orders`, `GET /api/orders/{id}`, cancelling one's own order, confirming a parcel arrived, `GET /api/shop-applications/mine` | Any signed-in user - always **their own**; somebody else's is **404** |
| `POST /api/shop-applications`, `PUT /api/products/{id}/reviews/mine` | `Customer` |
| Product writes (`POST /api/products`, variants, prices, translations, images, delete), `PUT /api/stock/{variantId}` | `Seller`, `Admin` - a seller's write to somebody else's product is **404** (checked in the handler) |
| `GET /api/products/mine`, `/api/sellers/me`, `/api/orders/sales/*` | `Seller` |
| `GET /api/users`, lock and unlock, the shop-application queue and decisions, the product review queue and decisions, review hiding, `GET /api/audit/mine` | `Admin`, `Moderator` |
| Granting and revoking `Moderator`, ban and lift, `/api/users/lookup` and `/stats`, categories, fulfilment, the staff order read and cancel, payouts, reservations, payments, the audit log, insights, orphan images | `Admin` |

The [Bruno collection](../../../bruno/) exercises every row, including the 401 / 403 / 404 cases in
`security-checks/`.

Two kinds of refusal come from handlers rather than attributes: **404** when the thing is not the
caller's (a 403 would confirm it exists), and **403 with a readable sentence** through
`Ecommerce.Shared.Exceptions.ForbiddenException` when the caller is known and the answer is no - a
locked account after the right password, a moderator asking to lock another moderator or for more than
30 days.

> Because roles live inside the token, a granted or revoked role, an approved shop and a lock or ban
> reach a session only when it is **refreshed** - `RefreshTokenCommandHandler` re-reads the account and
> its roles every time. A lock or ban revokes every refresh token at once, and refresh refuses a locked
> or banned account whatever token it presents.
>
> **Since specs/065 the access token already issued stops too, within seconds** (#112). Identity
> publishes `AccessTokensRevoked(UserId, RevokedAt)` on a lock, a ban, a role revoked, a password reset
> or change, and a reused refresh token. `AddJwtAuthentication` registers a `RevokedAccessTokens`
> singleton and an `OnTokenValidated` hook that fails any token of that user whose `iat` is earlier.
> Every service feeds the singleton through `x.AddAccessTokenRevocations("<service>")`, which uses a
> **temporary queue per instance**, so every instance hears every revocation. A token issued in the same
> second is accepted, because `iat` has whole seconds only. After a password change or a role revoke,
> the refresh that follows succeeds and carries the new state; after a lock or ban it is refused.
