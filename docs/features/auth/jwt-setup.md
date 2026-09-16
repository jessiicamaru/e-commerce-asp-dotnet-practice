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
Add the following `JwtSettings` section to your [`appsettings.json`](file:///d:/Code/CSharp/e-commerce/server/src/Services/Identity/Ecommerce.Identity.WebApi/appsettings.json):

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
[`Ecommerce.Shared/Authentication/`](file:///d:/Code/CSharp/e-commerce/server/src/BuildingBlocks/Ecommerce.Shared/Authentication/),
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

Forgetting this in a new service means an empty secret and every token rejected with 401.
`AddJwtAuthentication` fails fast at startup rather than failing silently per request.

### 3.2 Three traps worth knowing

| Trap | What happens | How it is handled |
| :--- | :--- | :--- |
| **Inbound claim mapping** | `JwtSecurityTokenHandler` rewrites `sub` to `ClaimTypes.NameIdentifier`, so looking up `"sub"` returns `null`. | `options.MapInboundClaims = false` keeps claim names verbatim. |
| **Role claim name** | The signing side adds `ClaimTypes.Role` (a long URI), but the handler **shortens it to `"role"`** when writing the token. Validating against the long URI rejects every administrator with `403`. | `RoleClaimType = "role"`. |
| **Clock skew** | The default 5-minute tolerance keeps a 15-minute token usable for 20. | `ClockSkew = TimeSpan.Zero`. |

The role trap is easy to miss because a hand-crafted test token signed with your *own* assumptions
will pass. It only shows up against a token the Identity service actually issued — which is why CI
runs [`verify-auth.sh`](file:///d:/Code/CSharp/e-commerce/.github/scripts/verify-auth.sh) against
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
(see [Database Schema Design](./db-design.md)) and travel inside the token.

| Endpoint | Access |
| :--- | :--- |
| `GET /api/products`, `GET /api/categories` | Anonymous (`[AllowAnonymous]`) |
| `POST /api/products`, `POST /api/categories` | `Admin` only |
| `POST /api/orders` | Any authenticated user |
| `POST /api/auth/*` | Anonymous |

> Because roles live inside the token, granting someone a role does **not** take effect until their
> current access token expires (15 minutes) or is refreshed. That is inherent to stateless JWT;
> instant revocation would need a token blacklist or much shorter lifetimes.
