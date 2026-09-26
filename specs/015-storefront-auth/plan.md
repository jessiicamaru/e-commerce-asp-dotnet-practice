# Implementation Plan: Sign Up, Sign In, Stay Signed In

> Written on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull request
> and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Branch**: `015-storefront-auth` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/015-storefront-auth/spec.md`

## Summary

The storefront's first real client of the token design. An `AuthProvider` keeps the user and the access
token in memory, hands the call layer from specs/014 a token provider and a refresh function through
`configureAuth`, restores the session on load with one `POST /api/auth/refresh`, and shares a single
in-flight refresh between concurrent 401s. Sign-in and sign-up pages, a `RequireAuth` guard that waits
for the restore, and a top bar that shows who is signed in. Building it found that nothing could end a
session, so Identity gains `POST /api/auth/logout`: anonymous, idempotent, always 204, deleting the
refresh token row and clearing the cookie. Registration's missing validation is filed as #43.

## Technical Context

**Language/Version**: TypeScript ~6.0 / React 19 (client); C# 13 / .NET 10.0 (Identity)

**Primary Dependencies**: `react-router-dom` (client); MediatR 12.4.1, EF Core with Npgsql (Identity)

**Storage**: Identity's `refresh_tokens` table in `ecommerce_identity_db` (host port 5435) - no schema
change; logout deletes one row

**Testing**: `Ecommerce.Identity.Tests/SessionTests` against a real PostgreSQL; Bruno `auth/logout` and
`auth/refresh after logout is 401`; the HTTP flow through the Vite proxy with curl and a cookie jar

**Target Platform**: The browser via Vite (`:5173`), Identity on `:5056` behind the gateway

**Project Type**: Web front end plus one backend endpoint

**Performance Goals**: None stated.

**Constraints**: No token in any script-readable storage; a sign-out must end the session on the
server; a wrong password must not reveal whether the email exists (#28).

**Scale/Scope**: 3 client pages/components, 1 context, 1 command + handler, 1 controller action, 1 test
class, 2 Bruno requests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0, after the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client talks only to the gateway. The logout endpoint lives in Identity, which owns sessions; no other service is involved |
| **II. Clean Architecture Layering** | **Pass.** `LogoutCommand` and its handler sit in `Application/Auth/Commands/Logout/` and use the existing `IUserRepository`; the controller action reads the cookie, dispatches through MediatR, and clears the cookie - transport concerns only |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** One `SaveChangesAsync` removes the token. No message is published. Repeating logout is harmless by construction: a missing or unknown token returns before any write, which `Logging_out_without_a_valid_session_is_a_quiet_no_op` covers |
| **IV. Identity Comes From the Token** | **Pass on identity; one deviation on declaration.** Logout identifies the session only by the HttpOnly cookie, never by a body field, and takes no user id. It is anonymous on purpose - an expired access token must not stop a sign-out - and the action's comment says so. But the principle asks for anonymous access to be *declared* with `[AllowAnonymous]` on top of authenticated-by-default, and neither half holds here: `AuthController` carries no `[Authorize]` and no `[AllowAnonymous]`, and no fallback policy makes endpoints authenticated by default. Logout follows the pattern register, login and refresh already had; see Complexity Tracking |
| **V. Evidence Over Assumption** | **Pass, with the gap named.** The flow was exercised through the Vite proxy with a real cookie jar (register 200 with the cookie, refresh 200, logout 204 clearing it, refresh 401) and `SessionTests` runs against real PostgreSQL. The pull request says plainly that nobody clicked through the pages in a real browser |

**Post-design re-check**: one deviation, recorded in Complexity Tracking. The backend change is additive (a new endpoint, no schema
change), and the one missing capability that could not be fixed here without scope creep -
registration validation - was filed (#43) rather than papered over in the client.

## Project Structure

### Documentation (this feature)

```text
specs/015-storefront-auth/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Five decisions
├── data-model.md        # refresh_tokens (unchanged schema) and the client's session state
├── quickstart.md        # The cookie-jar run, the tests, Bruno
├── contracts/
│   └── http-api.md      # POST /api/auth/logout (new) and the auth endpoints the client relies on
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/Services/Identity/
├── Ecommerce.Identity.Application/Auth/Commands/Logout/LogoutCommand.cs, LogoutCommandHandler.cs
└── Ecommerce.Identity.WebApi/Controllers/AuthController.cs            # [HttpPost("logout")]
server/tests/Ecommerce.Identity.Tests/SessionTests.cs
bruno/auth/logout.yml, bruno/auth/refresh after logout is 401.yml

client/src/
├── auth/AuthContext.tsx      # AuthProvider: token in memory, restore, shared refresh, sign in/up/out
├── auth/useAuth.ts           # User, AuthResponse, AuthState, useAuth()
├── auth/RequireAuth.tsx      # waits for `restoring`, then redirects with state.from
├── pages/SignInPage.tsx, SignUpPage.tsx
├── App.tsx                   # AuthProvider, routes /sign-in /sign-up /account, the top bar
└── index.css
docs/features/auth/jwt-setup.md, docs/features/auth/security-best-practices.md
```

**Structure Decision**: auth state gets its own folder (`src/auth/`) because both the pages and the
call layer depend on it; the call layer stays ignorant of React and learns the token through
`configureAuth`, the seam specs/014 left for it.

## Complexity Tracking

| Deviation | Why it was accepted | Simpler alternative rejected |
| :--- | :--- | :--- |
| `POST /api/auth/logout` is anonymous by the absence of `[Authorize]`, not by an explicit `[AllowAnonymous]` (principle IV) | Not recorded as a decision: the action copied the controller's existing shape, where register, login and refresh are anonymous the same way. The behaviour is the intended one; only its declaration departs from the rule | Adding `[AllowAnonymous]` to the action would have matched the principle with no change in behaviour. Found while writing this record on 2026-09-27, not at the time |

## What this feature does not finish

- **Registration still accepts anything** (#43).
- **An access token already issued survives logout** for up to its 15 minutes - revocation arrived with
  specs/065.
- **No client tests**: the provider's shared refresh and the guard's wait are checked by type and by
  reading, not by a test (the client had none until specs/028).
- **Not clicked through in a real browser** at this merge.
