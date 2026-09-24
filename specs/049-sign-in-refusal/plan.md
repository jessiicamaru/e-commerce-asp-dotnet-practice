# Implementation Plan: Sign-in refusal

**Branch**: `049-sign-in-refusal` | **Spec**: [spec.md](spec.md) | **Issue**: #120

## Technical context

- `Ecommerce.Shared/Exceptions/ForbiddenException` is a message only.
  `Ecommerce.Shared/Middlewares/GlobalExceptionHandler` maps it to 403 and shows the message
  (`ShowMessage`).
- `LoginCommandHandler` throws `ForbiddenException(why)` after the right password, for `IsBanned` or
  `IsLocked(now)`.
- Client: `pages/sign-in/index.tsx`. `ApiError.problem` is the ProblemDetails body.

## Decisions

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

## Constitution check

- I (autonomy): nothing crosses a service. Pass.
- IV (identity from the token): unchanged. The refusal still comes only after the right password.
  Pass.
- V (evidence): the client test fails first on the current page. The server test asserts the facts on
  the exception, and a handler test asserts they reach the HTTP body outside Development. Bruno asserts
  them end to end. Pass.

## Files

- `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/ForbiddenException.cs`
- `server/src/BuildingBlocks/Ecommerce.Shared/Middlewares/GlobalExceptionHandler.cs`
- `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`, a new `ForbiddenProblemTests.cs`
- `bruno/admin-users/the locked customer cannot sign in.yml`
- `client/src/config/axios/api-error.ts` (extension fields on `ProblemDetails`)
- `client/src/pages/sign-in/refusal.ts`, `index.tsx`, `index.test.tsx`; `client/src/test/refusal.ts`
- `client/src/locales/{en,vi}/auth.json`
- Docs: `docs/features/moderation-and-staff.md`, `docs/architecture/error-handling-and-shared-building-block.md`, `docs/project/*`, the generated reference if it changes
