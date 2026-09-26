# Research: Emails for what happens to people

> Written on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-26

Seven decisions. D1-D5 are the spec's Decisions (D1 is [decisions.md row 65](../../docs/project/decisions.md));
D6 and D7 are read from the code.

---

## D1 - The reader's language is learnt from use, not set

**Decision**: `users.Language`, recorded from `Accept-Language` at registration (both kinds), at sign-in and at
every session renewal. An email about an order uses the order's language; any other uses `users.Language`, else
Vietnamese.

**Rationale**: the issue left two options. The language of the request that caused the email cannot work for back
in stock, which no request of the reader's causes, nor for a lock, which a moderator's request causes. A setting is
a screen nobody visits. The storefront already sends `Accept-Language` on every request, and it renews the session
every few minutes, so a switch reaches the account quickly.

**Alternatives considered**:

- **The causing request's language.** Rejected: there is no such request of the reader's for back in stock or a
  lock (pull request).
- **A language setting on the account page.** Rejected: a setting is a screen nobody visits; out of scope.

---

## D2 - A renewal records it with one guarded statement

**Decision**: `RefreshTokenCommandHandler` calls `RecordLanguageAsync(userId, language)` only when the supported
language differs from the loaded one, and the repository runs
`UPDATE users SET "Language" = @l WHERE "Id" = @id AND ("Language" IS NULL OR "Language" <> @l)`. Sign-in sets the
tracked entity, which is saved with the sign-in; registration sets it on the new row.

**Rationale**: a renewal happens every few minutes per open tab; the cost must be nothing when nothing changed.

**Alternatives considered**: not recorded.

---

## D3 - `ReadersLanguage` lets a sender that cannot know ask Identity to decide

**Decision**: `EmailTemplate.ReadersLanguage = ""`. `QueueEmailCommandHandler` reads the recipient's
`users.Language` when the request's language is blank, and falls back to `EmailTemplates.DefaultLanguage` (`vi`).

**Rationale**: the reader's language is a fact about an account, which Identity owns (Principle I). Catalog knows a
saver only by id. Copying the language into every service would be a second source of truth that goes stale.

**Alternatives considered**:

- **Publish the language with the saved-product or user events so Catalog can keep a copy.** Not recorded as
  considered; rejected here for the reason above.

---

## D4 - Returns are three emails, not one

**Decision**: `ReturnAccepted`, `ReturnRefused` (with `{reason}`), `ReturnRefunded` (with `{amount}`).

**Rationale**: the issue named a single `ReturnDecided`. An editable template with a condition inside it cannot be
edited sensibly; three templates each have their own words.

**Alternatives considered**: `ReturnDecided` with a condition - rejected for the reason above.

---

## D5 - What the words say

**Decision**: the cancellation email says the whole total is refunded and does not say who cancelled; a lock's end
time is written in UTC and says so; there is no opt-out.

**Rationale**: specs/039 refunds a cancelled order in full, whoever cancels; one wording serves both actors. An
email has no reader's time zone. These are transactional, and back in stock stops when the product is no longer
saved.

**Alternatives considered**: per-actor cancellation wording - rejected as unnecessary.

---

## D6 - Asked for where the notice is, in the same transaction

**Decision**: each `SendAsync` sits beside the `NotifyAsync` for the same event: `OrderNotices.ShippedAsync` /
`CancelledAsync` (called from the guarded move's `stage`), the return steps' `stage` callbacks,
`UserAdministrationHandlers` before `SaveChangesAsync`, and `TellWhoSavedItAsync` inside the availability
consumer's outbox. Preparing a parcel is not news and asks for nothing.

**Rationale**: Principle III. A request staged in the transaction that a guarded `UPDATE` decided is published only
if that `UPDATE` moved a row, which is what makes "a change that did not happen asks for nothing" true without a
separate check.

**Alternatives considered**: a consumer in Identity listening to the existing domain events and emailing from them.
Not recorded as considered.

---

## D7 - Only a language an email is written in is recorded

**Decision**: `EmailTemplates.Supported(tag)` trims, lower-cases, keeps the first two letters and returns them only
if they are `vi` or `en`; otherwise null, and nothing is written.

**Rationale**: a header asking for a language no email is written in would otherwise overwrite a good value with a
useless one. "en-US" counts as "en".

**Alternatives considered**: storing the header as sent. Rejected for the reason above.

---

## Also fixed in the same pull request

**A flaky insight test.** The first CI run was red on `InsightsTests.A_single_day_is_a_period`: 73,700 where
34,100 was placed. Each insight test drew one of 3,000 random days, and admin revenue sums every order on a day, so
two tests sometimes drew the same day and counted each other's orders. `InsightDays` now hands out days ten apart,
from a random base past the ranges other tests use (from the pull request).
