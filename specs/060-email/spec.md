# Feature Specification: Email

**Feature Branch**: `060-email` | **Created**: 2026-09-25 | **Issue**: #102

## Why

No service sends an email: not an order confirmation, and not the password reset (#103) or address
confirmation (#106) that come next. The in-app bell (specs/042) is the only way anybody is told
anything, and it reaches only somebody who is already signed in.

## User Scenarios

### US1 - A customer receives an order confirmation (P1)

When an order settles `Paid`, its customer receives one email. It confirms the order number, the total in
the order's currency, and a link to the order. It is written in the language the order was placed in
(`orders.Language`), the same language the order itself keeps (specs/021).

**Acceptance**
1. Placing an order puts exactly one confirmation into the local mail catcher, addressed to the
   customer, in their order's language.
2. A redelivered settlement, or a redelivered email request, sends no second email.

### US2 - A mail server that is down delays email, never loses it (P1)

**Acceptance**
1. With the mail server stopped, an order still settles `Paid`. The email is not part of what the order
   waits for.
2. When the mail server returns, the waiting email goes out, once, without anybody resending it.
3. An email that still cannot be sent after its retries is kept with its last error, so it can be seen.

### US3 - Nothing leaves the machine in development (P1)

Mailpit runs in `docker-compose.yml`, and every email the stack sends is readable at
`http://localhost:8025`. No real inbox is ever reached from a development stack.

## Requirements

- **FR-001**: `IEmailSender.SendAsync(recipientId, template, data, language)` in `Ecommerce.Shared`
  publishes `EmailRequested` through the caller's outbox, so an email about a change commits with the
  change (constitution III). It stores the request, never a rendered sentence, just as a notice does.
- **FR-002**: Identity, which knows email addresses, keeps each request in `outgoing_emails`. The row is
  idempotent on the email id.
- **FR-003**: A sweeper sends pending emails over SMTP. It retries with growing waits (1 minute, doubling
  to 1 hour, at most 12 attempts) and marks an email `Failed` with its last error once the attempts run
  out. It is safe on several instances.
- **FR-004**: Templates in Vietnamese and English. An unknown language falls back to the default,
  Vietnamese. An unknown template is `Failed`, never sent with holes in it.
- **FR-005**: An email to a person Identity does not know is `Failed` ("no such recipient"), not retried.

## Out of scope

- Which other notices become emails. The seam is built here; #103 and #106 are its next users.
- A person's preferred language stored on the account. The order's language answers it here.
- Unsubscribing, HTML design, attachments.

## Decisions

- **Identity sends, not Activity** (the issue suggested Activity). Identity is the one service that
  knows email addresses. Activity sending would need a read model of every address, and a backfill for
  every account created before it. #103 and #106 are Identity features.
- **A table and a sweeper, not a retry policy on the consumer.** In-memory retries give up after
  minutes and leave the email in an error queue nobody reads. A row with its own next-attempt time
  survives a restart and a mail server that is down for an hour. This is the same shape as the saga's
  timeout sweeper (specs/053).
