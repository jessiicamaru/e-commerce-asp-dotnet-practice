# Architecture Guide: Global Cross-Cutting Error Handling & Shared Building Blocks

This document describes the architectural design and implementation of **Global Cross-Cutting Error Handling** and the **`Ecommerce.Shared` Building Block** across our Monorepo Microservices.

---

## 1. Overview & Architectural Motivation

In a Database-per-Service Microservices architecture, each service operates as an autonomous process. However, maintaining a **predictable, standardized error response format (RFC 7807)** across all API endpoints is critical for frontend consumers (React, Mobile apps).

Instead of duplicating middleware code across microservices, we centralize reusable cross-cutting concerns into a shared building block library: **`Ecommerce.Shared`**.

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
                               └───────────────────────────┘
```

---

## 2. Shared Building Block Project Structure (`Ecommerce.Shared`)

**Location**: `server/src/BuildingBlocks/Ecommerce.Shared/`

```text
Ecommerce.Shared/
├── Authentication/
│   ├── JwtSettings.cs             # Binds "JwtSettings"; shared by the signing and validating sides
│   ├── DependencyInjection.cs     # AddJwtAuthentication(): JwtBearer validation + ICurrentUser
│   ├── ICurrentUser.cs            # The authenticated caller, exposed to the Application layer
│   └── CurrentUser.cs             # Reads the claims off IHttpContextAccessor
├── Behaviors/
│   └── ValidationBehavior.cs      # MediatR pipeline behavior for automatic DTO validation
├── Exceptions/
│   ├── NotFoundException.cs       # Thrown when a requested entity does not exist (HTTP 404)
│   └── ConflictException.cs       # Thrown when a unique constraint fails (HTTP 409)
└── Middlewares/
    └── GlobalExceptionHandler.cs  # ASP.NET Core 10 IExceptionHandler returning RFC 7807 JSON
```

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

### 3.4 Exception-to-Status Mapping

| Exception | Status | Title |
| :--- | :--- | :--- |
| `FluentValidation.ValidationException` | 400 | Validation Failed |
| `UnauthorizedAccessException` | 401 | Unauthorized |
| `NotFoundException` | 404 | Resource Not Found |
| `ConflictException` | 409 | Resource Conflict |
| anything else | 500 | Internal Server Error |

---

## 4. Environment-Aware Security Masking

The `GlobalExceptionHandler` enforces strict security boundaries based on the runtime environment:

* **Development Environment (`IsDevelopment() = true`)**:
  `ProblemDetails.Detail` contains the exact C# `exception.Message` and stack context to streamline developer debugging.
* **Production Environment (`IsDevelopment() = false`)**:
  `ProblemDetails.Detail` masks internal system messages with `"An error occurred while processing your request."` to prevent internal infrastructure disclosure to attackers.

---

## 5. How Microservices Register `Ecommerce.Shared`

Every microservice (`Catalog`, `Identity`, `Order`, `Inventory`) enables shared error handling with just 2 steps:

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

> The Catalog service still contains unused copies of `ValidationBehavior`, `GlobalExceptionHandler`
> and its own exception types under `Ecommerce.Catalog.*`. The `Ecommerce.Shared` versions are the
> live ones — `Program.cs` and `DependencyInjection.cs` reference those. Do not extend the copies.
