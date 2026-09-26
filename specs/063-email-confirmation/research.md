# Phase 0 Research: Email confirmation

> Written on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-25

Six decisions. D1 to D4 are the plan's numbered "Decisions"; D5 and D6 are in the pull request and the code. The
project-level record is decision 47 in [docs/project/decisions.md](../../docs/project/decisions.md).

---

## D1 - Existing accounts count as confirmed

**Decision**: The migration backfills `UPDATE users SET "EmailConfirmedAt" = "CreatedAt" WHERE "EmailConfirmedAt"
IS NULL`; `DataInitializer` confirms the administrator it seeds.

**Rationale**: Those accounts predate the rule, and some already hold shops; asking again would stop shops that
already trade, for no gain. The seeded administrator's address is configured by whoever runs the system - nobody
else could have typed it.

**Alternatives considered**: ask every existing account to confirm - rejected above; leave them null and treat
null as "unknown, allowed" - rejected: it would make "not confirmed" mean two things forever.

---

## D2 - Buying is not gated; selling is

**Decision**: Only applying to sell (403 `EmailNotConfirmed`) and approving an application (409) check the
confirmation. Browsing, the cart, checkout and order emails do not.

**Rationale**: An unconfirmed address costs its owner nothing when paying - the buyer is paying. A shop is a public
claim made in the address's name.

**Alternatives considered**: gate checkout too - rejected (out of scope in the spec): it would turn away paying
customers to protect nobody.

---

## D3 - The approval is gated, not the seller registration

**Decision**: `register-seller` still creates the account and a pending application in one step. Approving it
while the applicant is unconfirmed is 409, checked before the guarded decision; staff see
`ApplicantEmailConfirmed` on each application.

**Rationale**: Registering as a seller stays one step, and the application simply waits for the address, as the
moderator also waits for it. Refusing the registration would lose the application for a reason the person can fix
in a minute.

**Alternatives considered**: refuse `register-seller` until confirmed - rejected: the account does not exist yet,
so it cannot have confirmed anything.

---

## D4 - 24 hours, not 30 minutes

**Decision**: `ConfirmationTokens.Lifetime` is 24 hours (a reset link lives 30 minutes).

**Rationale**: A confirmation link grants nothing an attacker wants, and people open welcome emails late.

**Alternatives considered**: 30 minutes like a reset - rejected: many people would find an expired link and have
to ask again.

---

## D5 - The token never crosses the broker; it is staged with the account

**Decision**: `EmailConfirmations.StageAsync` adds the token's hash and an `OutgoingEmail` to the EF context
(`IOutgoingEmailRepository.Stage`, new) so they commit with the caller's one save - the new account's, or the
resend's. The template is in `ScrubbedOnceSent`.

**Rationale**: As with the reset link (specs/061), the token is a credential: no outbox row or queue may hold it,
and the sent row keeps no copy. Staging through EF rather than `QueueAsync`'s raw insert is what lets it join
registration's single save.

**Alternatives considered**: `IEmailSender` - rejected for the copies; `QueueAsync` inside an explicit transaction
- rejected: registration saves once and has no transaction of its own to join.

---

## D6 - Confirming is two guarded statements; resending is one a minute under a row lock

**Decision**: `ConfirmEmailCommand` claims the token (`UPDATE ... WHERE "UsedAt" IS NULL AND "ExpiresAt" > now
RETURNING "UserId"`), then `TryConfirmAsync` (`UPDATE users ... WHERE "EmailConfirmedAt" IS NULL`), then records
`EmailConfirmed` - in one transaction. An already-confirmed user spends the link and records nothing. Resend locks
the user's row (`SentSinceAsync`), returns if a link was made in the last minute, else deletes unused links and
stages a new one.

**Rationale**: Two submissions of one link confirm once; a confirmation twice records once. The lock makes five
resends at once send one email (a mutation without the lock was caught). It is the pattern of specs/062's reset
interval.

**Alternatives considered**: read-check-write - rejected for the race; no resend interval, relying on the gateway's
per-IP limit - rejected: the gateway limit is per client, not per account.
