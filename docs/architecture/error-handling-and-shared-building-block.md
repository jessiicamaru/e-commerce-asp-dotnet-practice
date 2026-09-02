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
                               └───────────────────────────┘
```

---

## 2. Shared Building Block Project Structure (`Ecommerce.Shared`)

**Location**: `server/src/BuildingBlocks/Ecommerce.Shared/`

```text
Ecommerce.Shared/
├── Behaviors/
│   └── ValidationBehavior.cs      # MediatR pipeline behavior for automatic DTO validation
├── Exceptions/
│   ├── NotFoundException.cs       # Thrown when a requested entity does not exist (HTTP 404)
│   └── ConflictException.cs       # Thrown when a unique constraint fails (HTTP 409)
└── Middlewares/
    └── GlobalExceptionHandler.cs  # ASP.NET Core 10 IExceptionHandler returning RFC 7807 JSON
```

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
using Ecommerce.Shared.Middlewares;

// 1. Add IExceptionHandler and ProblemDetails services
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// 2. Enable exception handling middleware in the HTTP pipeline
app.UseExceptionHandler();
```
