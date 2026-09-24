# Architecture Guide: Global Cross-Cutting Error Handling & Shared Building Blocks

This document describes the architectural design and implementation of **Global Cross-Cutting Error Handling** and the **`Ecommerce.Shared` Building Block** across our Monorepo Microservices.

---

## 1. Overview & Architectural Motivation

In a Database-per-Service Microservices architecture, each service operates as an autonomous process. However, maintaining a **predictable, standardized error response format (RFC 7807)** across all API endpoints is critical for frontend consumers (React, Mobile apps).

Instead of duplicating middleware code across microservices, we centralize reusable cross-cutting concerns into a shared building block library: **`Ecommerce.Shared`**. It began with error handling and validation and has since taken on everything every service must do *the same way*: authentication and the caller's identity, the request's language and currency, the audit trail, notifications and observability.

```text
                               ┌───────────────────────────┐
                               │     API Gateway (YARP)    │
                               └─────────────┬─────────────┘
                                             │
                      ┌──────────────────────┴──────────────────────┐
                      ▼                                             ▼
        ┌───────────────────────────┐                 ┌───────────────────────────┐
        │     Identity Service      │                 │      Catalog Service      │
        └─────────────┬─────────────┘                 └─────────────┬─────────────┘
                      │                                             │
                      └──────────────────────┬──────────────────────┘
                                             ▼
                               ┌───────────────────────────┐
                               │     Ecommerce.Shared      │
                               │  - GlobalExceptionHandler │
                               │  - ValidationBehavior     │
                               │  - Domain Exceptions      │
                               │  - JWT Authentication     │
                               │  - ICurrentUser           │
                               │  - StaffRoles             │
                               │  - IRequestLanguage       │
                               │  - IRequestCurrency       │
                               │  - IAuditTrail            │
                               │  - INotifier              │
                               │  - Observability          │
                               └───────────────────────────┘
```

---

## 2. Shared Building Block Project Structure (`Ecommerce.Shared`)

**Location**: `server/src/BuildingBlocks/Ecommerce.Shared/`

```text
Ecommerce.Shared/
├── Audit/
│   └── AuditTrail.cs              # IAuditTrail + AddAuditTrail(serviceName); AuditCategory; AuditSnapshot (redaction)
├── Authentication/
│   ├── JwtSettings.cs             # Binds "JwtSettings"; shared by the signing and validating sides
│   ├── DependencyInjection.cs     # AddJwtAuthentication(): JwtBearer validation + ICurrentUser; refuses incomplete settings
│   ├── ICurrentUser.cs            # The authenticated caller: Id, Email, GivenName, IsAuthenticated, IsInRole
│   ├── CurrentUser.cs             # Reads the claims off IHttpContextAccessor
│   └── StaffRoles.cs              # Admin, Moderator, and Staff = "Admin,Moderator" for [Authorize(Roles = ...)]
├── Behaviors/
│   └── ValidationBehavior.cs      # MediatR pipeline behavior for automatic DTO validation
├── Exceptions/
│   ├── NotFoundException.cs       # A requested entity does not exist - or is not the caller's (HTTP 404)
│   ├── ConflictException.cs       # The request conflicts with the current state (HTTP 409)
│   ├── ForbiddenException.cs      # The caller is known and the answer is no, with a reason to show (HTTP 403)
│   └── DependencyUnavailableException.cs  # A service this request depends on did not answer (HTTP 503)
├── Localization/
│   ├── DependencyInjection.cs     # AddRequestLanguage() / UseRequestLanguage(): ?lang=, then Accept-Language
│   └── RequestLanguage.cs         # IRequestLanguage, LanguageOptions ("Localization" section)
├── Middlewares/
│   └── GlobalExceptionHandler.cs  # ASP.NET Core 10 IExceptionHandler returning RFC 7807 JSON
├── Money/
│   ├── Currency.cs                # A currency code and its minor unit; rounds half away from zero
│   ├── DependencyInjection.cs     # AddRequestCurrency() / UseRequestCurrency(): ?currency=, then X-Currency
│   └── RequestCurrency.cs         # IRequestCurrency, CurrencyOptions ("Money" section)
├── Notifications/
│   └── Notifier.cs                # INotifier + AddNotifier(); NotificationKind
└── Observability/
    ├── ObservabilityExtensions.cs # AddObservability(serviceName): OpenTelemetry logs and traces to OTLP_ENDPOINT
    ├── OrderIdLogScopeFilter.cs   # MassTransit consume filter adding OrderId to every log line
    └── IgnoreIncomingTraceContextPropagator.cs  # the gateway ignores a client's traceparent
```

What each newer part is for:

| Part | Used by | What it guarantees |
| :--- | :--- | :--- |
| `ICurrentUser` | every service with protected endpoints | The caller's id comes from the validated token, never from the request body. `IsInRole` serves checks that depend on a row (a seller writing *this* product); `GivenName` (specs/046) signs a review and defaults to null, so a token issued before it existed, and every test double, still work. |
| `StaffRoles` | Identity, Catalog, Activity | `[Authorize(Roles = StaffRoles.Staff)]` names Admin-or-Moderator once (specs/043). What a moderator may do to *whom* depends on the target's row and is checked in the handler. |
| `IRequestLanguage` | Catalog, Order, Cart | The language a request asked for (`?lang=`, then `Accept-Language`), negotiated by `RequestLocalization`; responses carry `Content-Language` and `Vary: Accept-Language` (specs/021). |
| `IRequestCurrency`, `Currency` | Catalog, Order, Cart | The currency a request asked for (`?currency=`, then `X-Currency`); responses carry `X-Currency` and `Vary: X-Currency`. `Currency.Round` and `Currency.Fits` know that the dong has no minor unit (specs/022). Neither options type has a default list: a service that wants languages or currencies configures them, and one that does not, does not start. |
| `IAuditTrail` | Identity, Catalog, Order, Inventory, Payment | Publishes `AuditEntryRecorded` through the service's outbox; the actor comes from `ICurrentUser` (none: the system) with the most powerful role held, and snapshots are redacted before they leave the service - any property named like password, token, secret or hash (specs/041). |
| `INotifier` | Identity, Catalog, Order | Publishes `UserNotificationRequested` through the outbox: a kind and data, never a sentence, so the storefront words it in the reader's language (specs/042). |
| Observability | every service and the gateway | Logs and traces to Seq over OpenTelemetry when `OTLP_ENDPOINT` is set ([observability](../guides/observability.md)). |

Both `IAuditTrail` and `INotifier` publish, so they obey the outbox rule: call them before the one
`SaveChangesAsync`, or inside a repository's `stage` callback
([reliable messaging §2.1-2.3](./reliable-messaging-and-outbox-pattern.md#21-audit-entries-and-notifications-are-messages-too)).

`JwtSettings` deliberately lives here rather than in the Identity service: the side that signs tokens
and the sides that validate them read the *same* class, so the issuer, audience and key cannot drift
apart. See the [JWT Setup Guide](../features/auth/jwt-setup.md) for the validation details.

---

## 3. Standardized Error Response Format (RFC 7807 ProblemDetails)

All unhandled exceptions and validation failures are transformed into standardized **RFC 7807 `ProblemDetails` JSON**:

### 3.1 Validation Failure Payload (HTTP 400 Bad Request)
```json
{
  "type": "about:blank",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/categories",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00",
  "errors": {
    "Name": ["Category name is required."],
    "Slug": ["Category slug must be less than 150 characters."]
  }
}
```

### 3.2 Resource Not Found Payload (HTTP 404 Not Found)
```json
{
  "type": "about:blank",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Category with specified ID was not found.",
  "instance": "/api/categories/019183ab-4f21-7d12-8000-000000000000",
  "traceId": "00-8ca121ab90214a11b11e929d0e0e4736-00"
}
```

### 3.3 Unauthorized Payload (HTTP 401 Unauthorized)

Returned when a handler throws `UnauthorizedAccessException` — for example, a token that carries no
usable `sub` claim. Requests rejected by the JWT middleware itself never reach the handler and are
answered by ASP.NET Core with a bare `401` plus a `WWW-Authenticate` header.

```json
{
  "type": "about:blank",
  "title": "Unauthorized",
  "status": 401,
  "instance": "/api/orders",
  "traceId": "00-9df41b7788c34ee7b2ce929d0e0e4736-00"
}
```

### 3.4 Forbidden Payload (HTTP 403 Forbidden)

Returned when a handler throws `ForbiddenException` (specs/043): the caller is known and the answer is
no, for a reason they are allowed to read - a locked or banned account signing in with the right
password, or a moderator reaching past what moderators may do.

```json
{
  "type": "about:blank",
  "title": "Forbidden",
  "status": 403,
  "detail": "This account is locked until 2026-10-01 00:00 UTC: repeated spam reviews",
  "instance": "/api/auth/login",
  "traceId": "00-2c1f0e9a77b34da6a3ce929d0e0e4736-00"
}
```

**403 is not for "this is not yours".** Somebody else's order, address, product, sale or variant is a
**404**, worded exactly like one that does not exist, because a 403 confirms the id is real and
belongs to somebody. And sign-in answers 403 with the reason only **after** the right password:
before it, a locked account and a wrong password must look the same (#28).

### 3.5 Exception-to-Status Mapping

| Exception | Status | Title |
| :--- | :--- | :--- |
| `FluentValidation.ValidationException` | 400 | Validation Failed |
| `UnauthorizedAccessException` | 401 | Unauthorized |
| `ForbiddenException` | 403 | Forbidden |
| `NotFoundException` | 404 | Resource Not Found |
| `ConflictException` | 409 | Resource Conflict |
| `DependencyUnavailableException` | 503 | Service Unavailable — a service this request depends on did not answer (Catalog, Cart or Identity at checkout; Catalog when a seller sets stock) |
| anything else | 500 | Internal Server Error |

**"Anything else" includes a bare `System.Exception`.** Throwing one for a client mistake reports it
as a server fault — see §6.

---

## 4. Which Messages Are Shown

The `GlobalExceptionHandler` decides per exception type whether `ProblemDetails.Detail` carries the
exception's message:

* **The mapped domain refusals are always shown**, in every environment:
  `ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException` and
  `DependencyUnavailableException`. Handlers throw them with a sentence written for the caller -
  "Not sold in USD: Sony A7 IV", "The cart is empty", "Delivery address not found" - and specs/022
  requires a refusal to name what it refused. Until then these were masked outside Development too,
  which made every carefully worded refusal invisible in any deployed image.
* **Everything else is masked outside Development** with
  `"An error occurred while processing your request."`. An unmapped exception is an internal one, and
  its text can carry a connection string, a file path or a row nobody outside should see. In
  Development (`IsDevelopment() = true`) its message is shown to help debugging.

`UnauthorizedAccessException` is not in the shown list, so outside Development a 401 raised by a
handler carries the generic detail. Every response carries a `traceId` extension, and a validation
failure an `errors` extension.

---

## 5. How Microservices Register `Ecommerce.Shared`

Every service with HTTP endpoints enables shared error handling with just 2 steps:

### Step 1: Application Layer Registration (`DependencyInjection.cs`)
```csharp
using Ecommerce.Shared.Behaviors;

var assembly = typeof(DependencyInjection).Assembly;

// 1. Scan and register FluentValidation validators
services.AddValidatorsFromAssembly(assembly);

// 2. Register MediatR ValidationBehavior pipeline
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

### Step 2: WebApi Layer Registration (`Program.cs`)
```csharp
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Middlewares;

// 1. Add IExceptionHandler and ProblemDetails services
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 2. Add JWT validation and ICurrentUser (services with protected endpoints)
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// 3. Enable exception handling middleware in the HTTP pipeline
app.UseExceptionHandler();

// 4. Authentication before authorization, both before MapControllers()
app.UseAuthentication();
app.UseAuthorization();
```

### Optional parts, registered where a service needs them

```csharp
builder.Services.AddAuditTrail("catalog");                   // IAuditTrail - Identity, Catalog, Order, Inventory, Payment
builder.Services.AddNotifier();                              // INotifier   - Identity, Catalog, Order
builder.Services.AddRequestLanguage(builder.Configuration);  // IRequestLanguage - Catalog, Order, Cart
builder.Services.AddRequestCurrency(builder.Configuration);  // IRequestCurrency - Catalog, Order, Cart
builder.AddObservability("catalog");                         // every service and the gateway

// ...and in the pipeline, for the two request-scoped choices:
app.UseRequestLanguage();
app.UseRequestCurrency();
```

`AddJwtAuthentication` refuses to start when `JwtSettings:Issuer` or `JwtSettings:Audience` is empty
or the secret is shorter than 32 bytes (#30), and names each problem. Before that a service with no
`appsettings.json` started, reported healthy and rejected every token with 401.

---

## 6. Lessons recorded

- **One copy only.** Catalog once carried its own `ValidationBehavior`, `GlobalExceptionHandler` and
  exception types; they were deleted in `763b77a`. Do not reintroduce a per-service copy: two
  exception types with the same name in two namespaces compile, review clean, and fall through the
  shared handler to a 500, because it pattern-matches on `Ecommerce.Shared.Exceptions`.
- **`ValidationBehavior` is constrained `where TRequest : notnull`, on purpose.** It used to be
  `where TRequest : IRequest<TResponse>`. In MediatR 12 a command that returns nothing implements
  `IRequest`, a *different* interface, so the constraint could not be met and MediatR dropped the
  behavior without a word — every such command's validator was dead code. Found in feature 010 when
  the cart accepted a quantity of `-1`; `Ecommerce.Cart.Tests/ValidationTests` fails if it returns.
- **Bare `Exception`s are 500s.** Identity's register, login and refresh handlers threw them, so a
  duplicate email and a wrong password both answered 500 until #28. Throw `Ecommerce.Shared.Exceptions.*`
  or `UnauthorizedAccessException`; a bare `Exception` is always a server fault by the time it reaches
  the client.
