# Research: Sign-in refusal

> Written on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

Five decisions. D1 and D4 are the ones the pull request records as "decided on the user's behalf"; the
user was not asked.

---

## D1 - Facts on the shared exception, not a new Identity type

**Decision**: `ForbiddenException(string message, IReadOnlyDictionary<string, object?>? facts = null)` in
`Ecommerce.Shared`, exposing `Facts`. The shared `GlobalExceptionHandler` copies them into
`ProblemDetails.Extensions` when the exception is a `ForbiddenException`.

**Rationale**: A 403 whose reader deserves the details is exactly what `ForbiddenException` already is
(specs/043). The optional parameter keeps every existing `throw new ForbiddenException(message)` compiling
and unchanged.

**Alternatives considered**:

- **A new `AccountStoppedException` in Identity.** Rejected: it would need its own arm in the shared
  handler, and CLAUDE.md warns that a per-service exception type "falls through the shared handler to a
  500" - it compiles, reviews clean and fails only at runtime.

---

## D2 - Reserved keys win

**Decision**: Facts are added with `Extensions.TryAdd`, after the handler has written `traceId` (and
`errors` for a validation failure). A fact named like one of those is dropped, not renamed.

**Rationale**: `traceId` is how a support request finds the log line; a fact that could replace it would
let one careless caller hide it. Dropping is the quiet, safe choice - renaming would invent a field nobody
reads.

**Alternatives considered**:

- **Let the fact win.** Rejected for the reason above; `ForbiddenProblemTests.A_fact_cannot_hide_the_trace_id`
  holds it, and the mutation "a fact may overwrite `traceId`" turned it red.
- **Rename the colliding fact.** Rejected in the plan ("dropped, not renamed"); no further reason is recorded.

---

## D3 - `code` is a stable identifier; `detail` stays

**Decision**: The sign-in refusal sends `code` (`AccountLocked` / `AccountBanned`), `until` (locks only,
`DateTime` of kind UTC, serialised as ISO 8601 with `Z`) and `reason`, beside an unchanged English
`detail`.

**Rationale**: A client that knows `AccountLocked` words it. Anything older, such as Bruno, curl or a client
from before this change, still reads the English `detail`. Adding fields changes no existing field, so this
is not breaking. It is the same principle as notifications (specs/042): data, not sentences.

**Alternatives considered**:

- **Parse `detail` in the client.** Rejected in the pull request ("the page words the refusal from `code`,
  not by parsing `detail`"): the same principle as notifications - data, not sentences.
- No other alternative is recorded.

---

## D4 - The page words it; a helper decides

**Decision**: `client/src/pages/sign-in/refusal.ts` exports `describeSignInFailure(t, language, caught)`:
401 → `signIn.wrong`; 403 with `AccountLocked` + `until` + `reason` → `signIn.locked`; 403 with
`AccountBanned` + `reason` → `signIn.banned`; any other 403 with a `detail` → that `detail`; everything else
→ `signIn.failed`.

**Rationale**: A supporting file beside the index is the client convention. The page test drives it through
the real page (typing into the form, reading the alert), so the helper is tested where it is used.

**Alternatives considered**:

- **Show the server's `detail` for every 403.** Rejected: it is English and in UTC, which is the complaint
  (#120). It remains the fallback for a 403 with no code the page knows.
- No other alternative is recorded.

---

## D5 - Local time via `toLocaleString(i18n.language)`

**Decision**: `new Date(problem.until).toLocaleString(language)`.

**Rationale**: It is how every other date in the storefront is shown, so a lock reads like the rest of the
shop; the browser supplies the time zone.

**Alternatives considered**:

- None recorded. The page tests assert `new Date(until).toLocaleString(<language>)`, so they hold in
  whatever time zone runs them.
