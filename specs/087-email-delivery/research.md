# Research: Email delivery

> Written on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-27

Five decisions. D1 and D4 are the spec's Decisions; D1-D3 together are
[decisions.md row 68](../../docs/project/decisions.md). D5 is read from the code.

---

## D1 - Sent emails are listed too

**Decision**: the list takes any one state - `Failed` (default), `Pending`, `Sent`.

**Rationale**: the issue asked whether sent emails should be listed, since the list records who bought what. An
administrator already reads every order, so this discloses nothing new, and "did the confirmation go out?" is the
first support question.

**Alternatives considered**: failed only - rejected, it would leave the most common support question unanswered.

---

## D2 - Never the data

**Decision**: `OutgoingEmailResponse` has no field for `DataJson`; the list shows recipient, address, template,
language, state, attempts, last error, times and `canRetry`.

**Rationale**: a reset or confirmation email's data is a token (specs/061, 063). Leaving the field out of the record,
rather than blanking it for two templates, means a future template with a secret cannot leak by omission. The
mutation "the data returned in the response" was run and caught.

**Alternatives considered**: showing data for templates without secrets - not recorded as considered.

---

## D3 - A reset or confirmation link is never retried

**Decision**: `MayRetry(email) = Status == Failed && !ScrubbedOnceSent.Contains(Template)`; the command refuses those
two templates with a 409 in words before anything else.

**Rationale**: a reset link dies after 30 minutes; sending it later helps nobody, and the person asks for a new one.
`ScrubbedOnceSent` is the existing list of templates whose data is a secret (specs/061), so the rule and the scrub
cannot disagree.

**Alternatives considered**: retrying with a freshly issued token - not recorded as considered; it would be a
password reset the person did not ask for at that time.

---

## D4 - The retry's history is in the audit log

**Decision**: the retry records `AuditCategory.System`, action `EmailRetried`, subject `Email` / id, with "before"
`{ Status: "Failed", Attempts, LastError }` and "after" `{ Status: "Pending", Attempts: 0 }`.

**Rationale**: `outgoing_emails` keeps one current state; adding a history table for this would duplicate what the
audit log (specs/041) already does, and the retry clears exactly the fields the audit entry keeps.

**Alternatives considered**: an attempts history table - not recorded as considered.

---

## D5 - One guarded statement, inside the unit of work

**Decision**: `TryRetryAsync` is
`UPDATE outgoing_emails SET "Status" = 'Pending', "Attempts" = 0, "NextAttemptAt" = @now, "LastError" = NULL
WHERE "Id" = @id AND "Status" = 'Failed'`, true when it moved one row. The handler runs it and the audit entry inside
`IUnitOfWork.ExecuteInTransactionAsync` (which runs under the context's execution strategy, as
`EnableRetryOnFailure` requires) and saves once.

**Rationale**: of two administrators at once, or a retry of an email already back in the queue, exactly one moves it
(Principle III); the other gets 409 and no audit entry. The dispatcher needs nothing else: a `Pending` row due now
is what it sends.

**Alternatives considered**: load, check, set, save - rejected, two concurrent retries would both pass the check.
