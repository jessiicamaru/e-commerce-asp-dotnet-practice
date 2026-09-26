# Implementation Plan: Sign-in refusal

> Completed on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Branch**: `049-sign-in-refusal` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #120

## Summary

Give the 403 that Identity already sends a locked or banned account (specs/043) its facts as data, and let
the storefront's sign-in page word them. The shared `ForbiddenException` gains an optional `Facts`
dictionary; the shared `GlobalExceptionHandler` copies it into the ProblemDetails extensions, never over
its own keys; `LoginCommandHandler` fills `code`, `until` and `reason`; and `pages/sign-in/refusal.ts`
turns them into a sentence in the reader's language and time zone. Nothing before the password check
changes, so #28 (no account enumeration) holds.

## Technical Context

- `Ecommerce.Shared/Exceptions/ForbiddenException` is a message only.
  `Ecommerce.Shared/Middlewares/GlobalExceptionHandler` maps it to 403 and shows the message
  (`ShowMessage`).
- `LoginCommandHandler` throws `ForbiddenException(why)` after the right password, for `IsBanned` or
  `IsLocked(now)`.
- Client: `pages/sign-in/index.tsx`. `ApiError.problem` is the ProblemDetails body.

**Language/Version**: C# 13 / .NET 10.0 (server); TypeScript with React 19 and Vite (client)

**Primary Dependencies**: MediatR 12.4.1, ASP.NET Core ProblemDetails, `Ecommerce.Shared`; client
`react-i18next`, axios, Vitest with Testing Library

**Storage**: none new - the facts are read from the existing `users` columns `LockedUntil`, `LockReason`,
`BanReason` (PostgreSQL, `ecommerce_identity_db`, 5435)

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Identity.Tests`), a handler test that runs
`GlobalExceptionHandler` with a Production environment, Vitest page tests, and one Bruno request

**Target Platform**: Identity service (5056) behind the gateway (5000); the storefront

**Constraints**: #28 - before the right password, a locked account and a wrong password must look the same;
the change must be additive so clients that read only `detail` are unaffected

**Scale/Scope**: one exception type, one handler branch, one command handler, one page

## Decisions

Recorded in full, with alternatives, in [research.md](research.md).

**D1 - Facts on the shared exception, not a new Identity type.** `ForbiddenException` gains an optional
`Facts` dictionary, and the handler copies it into `Extensions`. A 403 whose reader deserves the details
is exactly what `ForbiddenException` already is (specs/043). A second exception type would need its own
arm in the handler, and CLAUDE.md warns against a per-service copy that "falls through the shared handler
to a 500".

**D2 - Reserved keys win.** `traceId` and `errors` are written by the handler and are never overwritten
by a fact. A fact with such a name is dropped, not renamed.

**D3 - `code` is a stable identifier; `detail` stays.** A client that knows `AccountLocked` words it.
Anything older, such as Bruno, curl or a client from before this change, still reads the English
`detail`. Adding fields changes no existing field, so this is not breaking.

**D4 - The page words it; a helper decides.** `pages/sign-in/refusal.ts` sits beside the index (client
convention: a supporting file beside the index). It maps an `ApiError` to a sentence, and the page test
drives it through the real page.

**D5 - Local time via `toLocaleString(i18n.language)`**, as every other date in the storefront is shown.

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) before design, re-checked
after.*

- I (autonomy): nothing crosses a service. Pass.
- IV (identity from the token): unchanged. The refusal still comes only after the right password.
  Pass.
- V (evidence): the client test fails first on the current page. The server test asserts the facts on
  the exception, and a handler test asserts they reach the HTTP body outside Development. Bruno asserts
  them end to end. Pass.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity owns the lock and ban columns and is the only service that reads them; nothing crosses a service boundary. `ForbiddenException` lives in `Ecommerce.Shared`, which the principle names as the one place for cross-cutting infrastructure |
| **II. Clean Architecture Layering** | **Pass.** The facts are built in the Application layer (`LoginCommandHandler`), which already threw the exception; the HTTP shape is decided in the shared middleware. The controller is untouched |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, not engaged.** No new write and no new message. The refusal's existing audit entry (`SignInRefused`) is still recorded and saved before the throw, as in specs/041 |
| **IV. Identity Comes From the Token** | **Pass.** Sign-in is the anonymous endpoint that establishes identity; the facts are shown only once the password has proved who is asking, and the 401 before it is unchanged (#28) |
| **V. Evidence Over Assumption** | **Pass.** 6 of the 8 new page tests failed on the old page; `ForbiddenProblemTests` runs the real handler with a Production environment and parses the body; Bruno asserts the fields through the gateway; six mutations were each caught (see [tasks.md](tasks.md)) |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/049-sign-in-refusal/
├── spec.md
├── plan.md                  # This file
├── research.md              # D1-D5 with rejected alternatives
├── data-model.md            # No table changed; where the facts are read from
├── quickstart.md            # Server, client and end-to-end checks
├── contracts/
│   └── http-api.md          # The 403 of POST /api/auth/login, before and after
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/ForbiddenException.cs`
- `server/src/BuildingBlocks/Ecommerce.Shared/Middlewares/GlobalExceptionHandler.cs`
- `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`, a new `ForbiddenProblemTests.cs`
- `bruno/admin-users/the locked customer cannot sign in.yml`
- `client/src/config/axios/api-error.ts` (extension fields on `ProblemDetails`)
- `client/src/pages/sign-in/refusal.ts`, `index.tsx`, `index.test.tsx`; `client/src/test/refusal.ts`
- `client/src/locales/{en,vi}/auth.json`
- Docs: `docs/features/moderation-and-staff.md`, `docs/architecture/error-handling-and-shared-building-block.md`, `docs/project/*`, the generated reference if it changes

**Structure Decision**: the exception stays single and shared (D1); the wording is a supporting file beside
the page's `index.tsx`, following [client/README.md](../../client/README.md).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Refresh still answers a locked account with a plain 401; the person meets the explanation only on the
  sign-in page.
- An access token issued before the lock kept working until it expired (#112). That was closed later by
  specs/065, not here.
