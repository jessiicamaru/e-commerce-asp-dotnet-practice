---
description: "Task list for Signed in unless an endpoint says otherwise"
---

# Tasks: Signed in unless an endpoint says otherwise

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - `EndpointAccess` and the per-service tests ran against the unchanged code
(the survey, research D3) before any attribute changed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The test and the survey (US3)

- [X] T001 [US3] `EndpointAccess.Undeclared` in `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/EndpointAccess.cs`
- [X] T002 [P] [US3] `EndpointAccessTests.cs` in the seven service test projects; WebApi references for Identity, Payment, Cart, Activity
- [X] T003 [US3] Run them before any change: Identity red naming `AuthController`'s eight public actions, six services green (research D3)
- [X] T004 [US3] A test of the helper itself: a controller that says nothing is named, declared ones are not (`server/tests/Ecommerce.Identity.Tests/EndpointAccessTests.cs`)

## Phase 2: Fail closed (US1)

- [X] T005 [US1] `FallbackPolicyTests` through `TestServer` (`Microsoft.AspNetCore.TestHost` 10.0.11) in `server/tests/Ecommerce.Identity.Tests/`
- [X] T006 [US1] `SignedIn` fallback policy in `AddJwtAuthentication` (`server/src/BuildingBlocks/Ecommerce.Shared/Authentication/DependencyInjection.cs`)

## Phase 3: Stay public where public (US2)

- [X] T007 [US2] `AuthController`: class `[Authorize]`, `[AllowAnonymous]` on its eight public actions
- [X] T008 [P] [US2] `.AllowAnonymous()` on `/health`, OpenAPI, gRPC health and reflection in the seven `Program.cs`
- [X] T009 [P] [US2] `[AllowAnonymous]` on `CatalogPricingService` and `CatalogOwnershipService`

## Phase 4: Verification and docs

- [X] T010 Mutation checks (quickstart Scenario 6) - each red
- [X] T011 All seven service test suites; Bruno and `verify-saga.sh` against the seven containers rebuilt from this branch
- [X] T012 Docs: `docs/features/auth/jwt-setup.md`, `docs/features/auth/security-best-practices.md`, `docs/project/backlog.md`, `docs/project/timeline.md`, CLAUDE.md
- [ ] T013 Merged as #188, closing #183

## Verification

- Before: Identity `EndpointAccessTests` red - `AuthController.ConfirmEmail`, `ForgotPassword`, `Login`, `Logout`,
  `Refresh`, `Register`, `RegisterSeller`, `ResetPassword`; the six other services green.
- After: the new tests 12/12 in Identity (fallback, access, JWT startup); every service's `EndpointAccessTests` green.
- Mutations, each restored: no fallback - `An_endpoint_that_says_nothing_refuses_an_anonymous_caller` red; no class
  `[Authorize]` - `Every_action_says_who_may_call_it` red; helper ignores the controller - both Identity access tests red.
