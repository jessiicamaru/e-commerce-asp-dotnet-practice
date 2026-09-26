# Research: Signed in unless an endpoint says otherwise

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #183

---

## D1 - A fallback policy in `AddJwtAuthentication`, and explicit attributes held by a test

**Decision**: `services.AddAuthorization(o => o.FallbackPolicy = SignedIn)` inside the shared
`AddJwtAuthentication`, where `SignedIn` requires an authenticated user; `[Authorize]` on `AuthController` with
`[AllowAnonymous]` on its public actions; `EndpointAccess.Undeclared` asserted empty in every service's tests.

**Rationale**:

- Principle IV already states "authenticated by default; anonymous access is declared explicitly". The fallback is
  that sentence in code; the attributes and the test are the "declared explicitly" half.
- Only a fallback fails closed for **every kind** of endpoint. A per-controller `[Authorize]` protects that
  controller; the next controller, a new gRPC service or a `MapGet` would be public by the same omission.
- Putting it in `AddJwtAuthentication` means every service that validates tokens gets it with no new line - the
  place the constitution names, and the one a new service already has to call.
- The cost measured by the survey (D3) was small: one controller and a dozen mappings.

**Alternatives considered**:

- **Per-controller only** (`[Authorize]` on `AuthController`). Rejected as the whole fix: it closes this controller
  and leaves the default open everywhere else. It is part of the chosen fix, not an alternative to it.
- **A fallback only, no test.** Rejected: an action would still be private by an implication two projects away; the
  controller should say what it is.
- **A test only, no fallback.** Rejected: the test covers controller actions; a gRPC service or minimal endpoint
  would still be public by omission, and a test is only as good as the day it last ran.
- **A startup check that refuses to start when an endpoint declares nothing.** Rejected: with the fallback such an
  endpoint is already private, so a crash would add an outage for no protection.

---

## D2 - Which endpoints say "anonymous", and how

**Decision**:

| Endpoint | Where | How |
| :-- | :-- | :-- |
| register, register-seller, login, forgot-password, reset-password, confirm-email, refresh, logout | `AuthController` | `[AllowAnonymous]` |
| `/health` | Identity, Catalog, Cart, Order, Inventory, Payment, Activity `Program.cs` | `.AllowAnonymous()` |
| gRPC health, gRPC reflection | Identity, Catalog, Cart | `.AllowAnonymous()` |
| OpenAPI (Development only) | the seven services | `.AllowAnonymous()` |
| `CatalogPricingService`, `CatalogOwnershipService` | Catalog gRPC | `[AllowAnonymous]` on the class |

**Rationale**: Each was reached without a token before and must be after: compose and the gateway probe `/health`
without one; Order prices a checkout and Inventory asks ownership without forwarding a token (unlike `CartReading`
and `AddressReading`, which are `[Authorize]` and receive the customer's). The attribute sits on the thing, with a
comment saying why, rather than in a list in `Program.cs`.

**Alternatives considered**:

- **Forward the customer's token to `CatalogPricing` and make it `[Authorize]`.** Rejected here: a behaviour change
  on the checkout path, and the anonymous storefront's prices are served by REST, not gRPC, so it would protect
  nothing a caller can reach through the gateway. Out of scope (service identity).
- **Map health checks before `UseAuthorization`.** Rejected: fragile ordering that the next edit breaks; saying
  `.AllowAnonymous()` is explicit.

---

## D3 - The survey: who relied on the open default

**Decision**: Record what was found, because it set the size of D1.

**Findings** (every `*Controller.cs` under `server/src`, and every `Map*` in each `Program.cs`):

- 7 services call `AddJwtAuthentication`: Identity, Catalog, Order, Inventory, Payment, Cart, Activity. The
  Orchestrator and the gateway do not.
- Controllers with a class-level `[Authorize]`: 19. Controllers without: 7 - Catalog's `Products`, `Categories`,
  `Questions`, `Reviews`, Inventory's `Stock`, Activity's `NotificationWording` and Identity's `Auth`.
- Of those seven, **only `AuthController` had actions declaring nothing** (8 of 12). The other six declare each
  action one by one - confirmed by running `EndpointAccess.Undeclared` against every service before any change: only
  Identity's test failed.
- Non-controller endpoints: `/health` ×7, gRPC services ×4 (2 `[Authorize]`, 2 not), gRPC health and reflection ×3
  each, OpenAPI ×7.

**Rationale**: The fallback's blast radius is the list above, all handled in D2.

**Alternatives considered**: none - this is a measurement.

---

## D4 - The test reads attributes by reflection, per service

**Decision**: `EndpointAccess.Undeclared(Assembly)` in `Ecommerce.Shared.Authentication` scans public, non-abstract
`ControllerBase` types for public declared instance methods that are not `[NonAction]` or special names, and reports
those with neither `IAuthorizeData` nor `IAllowAnonymous` on the action or (inherited) the controller. Each service's
test project references its WebApi (four gained the reference) and asserts the list is empty.

**Rationale**: Reflection needs no running host, no database and no configuration, so the test runs in milliseconds
in every service. Reading `IAuthorizeData` / `IAllowAnonymous` rather than the concrete attributes also recognises
`[Authorize(Roles = ...)]` and any custom attribute built on them.

**Alternatives considered**:

- **Enumerate `EndpointDataSource` from a started host** (`WebApplicationFactory<Program>`). Rejected: every service
  would need its database, broker and configuration to start; the fallback test (below) covers the pipeline once.
- **One test project referencing all seven WebApis.** Rejected: seven `Program` types and seven dependency graphs in
  one test project for one assertion; a line per service is simpler and keeps each service's tests its own.

---

## D5 - The fallback is tested through a real pipeline once

**Decision**: `FallbackPolicyTests` (in `Ecommerce.Identity.Tests`, beside `JwtStartupTests`, because
`Ecommerce.Shared` has no test project) starts a `WebApplication` on `TestServer`, configured only by
`AddJwtAuthentication`, with one endpoint that says nothing and one that says `.AllowAnonymous()`; it asserts 401
anonymous, 200 with a signed token, and 200 anonymous for the public one.

**Rationale**: Asserting `options.FallbackPolicy != null` would pass for a policy that allows everybody. Sending
requests proves the behaviour. `Microsoft.AspNetCore.TestHost` 10.0.11 matches the framework packages already
referenced; `Mvc.Testing` 10.0.12 raised a package-downgrade error against `Microsoft.Extensions.Hosting` 10.0.11.

**Alternatives considered**:

- **Inspect `AuthorizationOptions`.** Rejected for the reason above.
