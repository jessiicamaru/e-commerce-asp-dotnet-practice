# Feature Specification: Signed in unless an endpoint says otherwise

**Feature Branch**: `089-explicit-anonymous` | **Created**: 2026-09-27 | **Issue**: #183

**Status**: Merged (#188, 2026-09-27)

**Input**: Issue #183 - "the auth endpoints are public only because they lack [Authorize]".

## Why

The constitution's Principle IV says: *"Endpoints are authenticated by default; anonymous access is declared
explicitly with `[AllowAnonymous]`."* No code enforced it:

- no service sets an authorization **fallback policy**, so an endpoint with no attribute is **public**;
- `AuthController` has no `[Authorize]` on the class and no `[AllowAnonymous]` anywhere. Sign-in, the two
  registrations, refresh, logout, forgot/reset password and confirm-email are anonymous **by omission**; the four
  signed-in actions (`me`, `me` PUT, `me/password`, `resend-confirmation`) carry `[Authorize]` one by one;
- an action added to that controller tomorrow is public, and looks ordinary in review - exactly the mistake
  Principle IV warns about.

A survey of every service (this record, research D3) found the same default everywhere, but only Identity's
`AuthController` actually relies on it: every other controller action already says `[Authorize]` or
`[AllowAnonymous]`, on itself or its controller. The non-controller endpoints - health probes, gRPC services,
gRPC health and reflection, OpenAPI - are public by omission too.

## User Scenarios & Testing *(mandatory)*

### US1 - An endpoint that says nothing is not public (Priority: P1)

Every service that validates tokens refuses an anonymous caller at any endpoint that does not declare itself
public. Forgetting fails closed.

**Why this priority**: It is the defect in its general form. Fixing `AuthController` alone would leave the next
controller, gRPC service or minimal endpoint public by the same omission.

**Independent Test**: In a real ASP.NET Core pipeline configured by `AddJwtAuthentication`, an endpoint with no
attribute answers 401 without a token and 200 with one; an endpoint marked `AllowAnonymous` answers 200 without.

**Acceptance Scenarios**:

1. **Given** a service using `AddJwtAuthentication`, **When** an anonymous request reaches an endpoint with no
   authorization metadata, **Then** it is refused with 401.
2. **Given** the same endpoint, **When** a caller with a valid token asks, **Then** it is served.
3. **Given** an endpoint marked `[AllowAnonymous]` / `.AllowAnonymous()`, **When** an anonymous caller asks,
   **Then** it is served.

---

### US2 - Every public endpoint says so, and nothing public today stops being public (Priority: P1)

`AuthController` is `[Authorize]` as a class, and each of its eight public actions says `[AllowAnonymous]`. Every
other public endpoint - health probes, gRPC health and reflection, OpenAPI, and Catalog's two service-to-service
gRPC services - is marked anonymous explicitly, so the fallback of US1 changes no behaviour a caller sees.

**Why this priority**: Equal to US1 - the fallback without this would turn sign-in, every container health check
and checkout's price lookup into 401s.

**Independent Test**: The whole Bruno collection, which calls every public endpoint anonymously, passes against
containers rebuilt from this branch; every container reports healthy; a checkout prices its lines.

**Acceptance Scenarios**:

1. **Given** no token, **When** a person registers, signs in, refreshes, signs out, asks for a reset, resets, or
   confirms an address, **Then** each answers as before.
2. **Given** no token, **When** compose or the gateway probes any service's `/health`, **Then** it answers 200.
3. **Given** Order pricing a checkout and Inventory checking ownership over gRPC without a token, **When** they
   call Catalog, **Then** Catalog answers as before.
4. **Given** no token, **When** `GET /api/auth/me` is asked, **Then** it is 401, as before.

---

### US3 - A test names any controller action that does not say who may call it (Priority: P2)

Each service's tests fail when one of its controller actions carries neither `[Authorize]` nor `[AllowAnonymous]`,
on itself or its controller, and the failure names it (`AuthController.Login`).

**Why this priority**: The fallback already keeps such an action private; this keeps the decision **in the source**,
where a reviewer reads it, and makes "public" a word somebody wrote.

**Independent Test**: Remove `[Authorize]` from `AuthController`'s class: Identity's test fails naming `Me`,
`UpdateMe`, `ChangePassword` and `ResendConfirmation`.

**Acceptance Scenarios**:

1. **Given** a controller action with no attribute on it or its controller, **When** the service's tests run,
   **Then** `EndpointAccessTests` fails and lists `Controller.Action`.
2. **Given** every action declared, **Then** the test passes - in all seven services that validate tokens.

### Edge Cases

- **Logout.** Stays anonymous on purpose: an expired access token must not stop somebody signing out (specs/029,
  its own comment). Now says so.
- **Refresh.** Anonymous: its credential is the HttpOnly cookie, not a bearer token.
- **gRPC with a forwarded token** (Cart's `CartReading`, Identity's `AddressReading`). Already `[Authorize]`; unchanged.
- **gRPC without one** (`CatalogPricing`, `CatalogOwnership`). Called service to service on the h2c port the
  gateway does not route; they were anonymous and stay so, now explicitly. Securing service-to-service calls is a
  separate question (Out of scope).
- **The Orchestrator and the gateway.** Neither calls `AddJwtAuthentication` - the orchestrator validates no
  tokens and the gateway forwards them - so neither gains a fallback; their `/health` stays public.
- **OpenAPI.** Mapped in Development only; marked anonymous so the document still opens there.
- **A token revoked (specs/065).** The fallback uses the same authentication, so a revoked token is refused at an
  undeclared endpoint as at an `[Authorize]` one.
- **An `[Authorize(Roles = ...)]` endpoint.** Unchanged: an endpoint's own policy replaces the fallback.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `AddJwtAuthentication` sets the authorization fallback policy to "an authenticated user", so every
  service that validates tokens is default-deny.
- **FR-002**: `AuthController` carries `[Authorize]` on the class and `[AllowAnonymous]` on register,
  register-seller, login, forgot-password, reset-password, confirm-email, refresh and logout.
- **FR-003**: Every non-controller endpoint that must stay public says so: `/health` (7 services), gRPC health and
  reflection (Identity, Catalog, Cart), OpenAPI (7 services), `CatalogPricingService` and `CatalogOwnershipService`.
- **FR-004**: `EndpointAccess.Undeclared(assembly)` lists the controller actions that declare neither, and a test in
  each of the seven services asserts it is empty; a test of the helper holds it to finding an undeclared action and
  ignoring declared ones.
- **FR-005**: No endpoint that was public before is refused after, and no endpoint that was signed-in becomes public.
- **FR-006**: No migration, no contract change, no gateway route change.

### Key Entities

- **Fallback policy**: ASP.NET Core's `AuthorizationOptions.FallbackPolicy` - applied to an endpoint that has no
  authorization metadata of its own.
- **Access declaration**: `[Authorize]` (any `IAuthorizeData`) or `[AllowAnonymous]` (`IAllowAnonymous`) on an action
  or its controller; `.AllowAnonymous()` / `.RequireAuthorization()` on a mapped endpoint.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `FallbackPolicyTests.An_endpoint_that_says_nothing_refuses_an_anonymous_caller` fails with the fallback
  removed and passes with it.
- **SC-002**: Identity's `EndpointAccessTests` failed before `AuthController` was changed, naming its eight public
  actions, and passes after; the other six services' pass.
- **SC-003**: The whole Bruno collection passes against the seven services rebuilt from this branch - every
  anonymous request in it still answers as before.
- **SC-004**: CI's `auth-smoke`, `saga-e2e` and `browser-e2e` jobs pass: sign-in, checkout (Catalog pricing over
  gRPC) and the storefront all work under the fallback.
- **SC-005**: Three mutations - no fallback, no class `[Authorize]` on `AuthController`, the helper ignoring the
  controller's attribute - each turn a test red.

## Decision

**Both, not either: a fallback policy in every service, and explicit attributes held by a test.** The issue offered
a fallback policy ("stronger, but touches every service's public reads") or per-controller attributes, plus a test in
either case. The fallback is what the constitution already requires and the only option that fails **closed** for the
next endpoint of any kind - a gRPC service or a minimal endpoint as well as a controller action. The survey (research
D3) showed its cost is small: every controller outside `AuthController` already declares each action, so only
`AuthController` and a dozen non-controller mappings needed a word. The test keeps the decision readable in the
controller rather than implied by a policy in another project. Recorded as decided on the user's behalf
([research.md](research.md) D1).

## Assumptions

- Every service that exposes endpoints and validates tokens calls `AddJwtAuthentication` and then
  `UseAuthentication()` / `UseAuthorization()` before mapping (true of all seven today).
- Catalog's gRPC port is reachable only inside the service network (compose publishes it for local debugging; the
  gateway routes REST only), as before this change.

## Out of scope

- Authenticating service-to-service gRPC calls (a service identity or mTLS). They were anonymous before and remain
  so, now visibly.
- Rate limits, CORS and anything else about the public endpoints themselves.
