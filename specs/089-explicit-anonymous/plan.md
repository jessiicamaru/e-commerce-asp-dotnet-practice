# Implementation Plan: Signed in unless an endpoint says otherwise

**Branch**: `089-explicit-anonymous` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #183

## Summary

Make Principle IV's "authenticated by default; anonymous declared explicitly" true in code. The shared
`AddJwtAuthentication` sets an authorization fallback policy requiring an authenticated user, so every service that
validates tokens is default-deny. `AuthController` becomes `[Authorize]` with `[AllowAnonymous]` on its eight
public actions; every non-controller endpoint that must stay public (health, gRPC health and reflection, OpenAPI,
Catalog's two service-to-service gRPC services) says `.AllowAnonymous()` / `[AllowAnonymous]`. A reflection helper,
`EndpointAccess.Undeclared`, backs a one-line test in each of the seven services that every controller action says
which it is.

## Technical Context

- `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/DependencyInjection.cs` (fallback) and a new
  `EndpointAccess.cs`.
- `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs`.
- Seven `Program.cs` files; `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/CatalogPricingService.cs`
  and `CatalogOwnershipService.cs`.
- Tests: `EndpointAccessTests.cs` in seven test projects; `FallbackPolicyTests.cs` in `Ecommerce.Identity.Tests`.

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: ASP.NET Core authorization (`AuthorizationOptions.FallbackPolicy`, `IAuthorizeData`,
`IAllowAnonymous`); `Microsoft.AspNetCore.TestHost` 10.0.11 (new, Identity tests only)

**Storage**: none

**Testing**: xUnit - reflection tests (no database) in every service; a `TestServer` pipeline test; Bruno whole
collection and `verify-saga.sh` against rebuilt containers; CI's auth, saga and browser end-to-end jobs

**Target Platform**: the seven services that call `AddJwtAuthentication`

**Performance Goals**: none - one policy evaluation per request that already ran authentication

**Constraints**: no endpoint may change who can reach it (FR-005)

**Scale/Scope**: 1 policy, 1 controller, 12 mappings in 7 `Program.cs`, 2 gRPC classes, 7 + 3 tests

## Design

- `DependencyInjection.SignedIn` - `new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`, public so a
  service can name it, and `services.AddAuthorization(o => o.FallbackPolicy = SignedIn)` in place of the bare
  `AddAuthorization()`.
- `AuthController`: class `[Authorize]`; `[AllowAnonymous]` above each public `[HttpPost]`; the four per-action
  `[Authorize]` removed as redundant.
- `Program.cs` ×7: `app.MapOpenApi().AllowAnonymous()`, `app.MapHealthChecks(...).AllowAnonymous()`, and in Identity,
  Catalog and Cart `MapGrpcHealthChecksService().AllowAnonymous()` / `MapGrpcReflectionService().AllowAnonymous()`.
- Catalog: `[AllowAnonymous]` on `CatalogPricingService` and `CatalogOwnershipService`, with the reason.
- `EndpointAccess.Undeclared(Assembly)` - see research D4.
- Test projects for Identity, Payment, Cart and Activity gain a reference to their WebApi (Catalog, Order and
  Inventory had one).

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service enforces its own fallback from its own configuration; no new call between services. Catalog's gRPC services stay reachable service to service exactly as before. |
| **II. Clean Architecture Layering** | **Pass.** Authorization stays in the WebApi layer (attributes, mappings) and the shared building block; Application and Domain are untouched. `EndpointAccess` lives in `Ecommerce.Shared.Authentication`, beside the registration it complements. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write, message or handler changes - stated rather than claimed as a pass. |
| **IV. Identity Comes From the Token** | **Pass - this feature is Principle IV's last sentence made true.** "Endpoints are authenticated by default; anonymous access is declared explicitly with `[AllowAnonymous]`" was stated and not enforced; the fallback enforces it and the test keeps the declarations explicit. |
| **V. Evidence Over Assumption** | **Pass.** The survey (research D3) was measured by running the helper against every service before any change; the pipeline test proves behaviour rather than configuration; three mutations were each caught; Bruno and `verify-saga.sh` ran against containers rebuilt from this branch. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/089-explicit-anonymous/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D5, including the survey
├── data-model.md        # No schema; the access decision before and after
├── quickstart.md
├── contracts/
│   └── http-api.md      # Every endpoint's declared access
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/DependencyInjection.cs`, `EndpointAccess.cs` (new)
- `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs`
- `server/src/Services/{Identity,Catalog,Cart,Order,Inventory,Payment,Activity}/Ecommerce.*.WebApi/Program.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/CatalogPricingService.cs`, `CatalogOwnershipService.cs`
- `server/tests/Ecommerce.*.Tests/EndpointAccessTests.cs` (7, new), `server/tests/Ecommerce.Identity.Tests/FallbackPolicyTests.cs` (new)
- `server/tests/Ecommerce.{Identity,Payment,Cart,Activity}.Tests/*.csproj` - the WebApi reference; Identity's also TestHost
- `CLAUDE.md`, `docs/features/auth/jwt-setup.md`, `docs/features/auth/security-best-practices.md`,
  `docs/project/backlog.md`, `docs/project/timeline.md`

No endpoint, message, table or gateway route changes, so `docs/reference/` is not regenerated. No new project, so the
Dockerfile is unchanged (test projects are not built into images).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Service-to-service gRPC (`CatalogPricing`, `CatalogOwnership`) is still unauthenticated - explicitly now. A service
  identity is its own feature.
- The reflection test covers controller actions only; a new minimal endpoint or gRPC service is covered by the
  fallback (fails closed) but not named by a test.
