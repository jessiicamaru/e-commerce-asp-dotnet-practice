# Feature Specification: Email

> Completed on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md).

**Feature Branch**: `060-email` | **Created**: 2026-09-25 | **Issue**: #102

**Status**: Merged (#143, 2026-09-25). Later work on the same seam: specs/061 (password reset), 063 (address
confirmation), 077 (editable templates, HTML), 083 (more emails), 087 (delivery screen). This record describes
the feature as it merged.

**Input**: Issue #102: the system sends no email.

## Why

No service sends an email: not an order confirmation, and not the password reset (#103) or address
confirmation (#106) that come next. The in-app bell (specs/042) is the only way anybody is told
anything, and it reaches only somebody who is already signed in.

## User Scenarios & Testing *(mandatory)*

### US1 - A customer receives an order confirmation (Priority: P1)

When an order settles `Paid`, its customer receives one email. It confirms the order number, the total in
the order's currency, and a link to the order. It is written in the language the order was placed in
(`orders.Language`), the same language the order itself keeps (specs/021).

**Why this priority**: It is the first email a shop is expected to send and the first user of the seam that the
password reset and address confirmation need next. Without a real user the seam would be untested.

**Independent Test**: Place and pay an order in English; Mailpit at `http://localhost:8025` holds one email to the
customer, "Your order xxxxxxxx is paid", with the total and a link to the order.

**Acceptance** (as first written)
1. Placing an order puts exactly one confirmation into the local mail catcher, addressed to the
   customer, in their order's language.
2. A redelivered settlement, or a redelivered email request, sends no second email.

**Acceptance Scenarios**:

1. **Given** an order placed in English, **When** the saga settles it `Paid`, **Then** one email reaches Mailpit
   addressed to the customer, subject "Your order {first 8 characters of the id} is paid", with the total in the
   order's currency and a link to `/orders/{id}` on the storefront.
2. **Given** an order placed in Vietnamese, **When** it settles, **Then** the email is in Vietnamese ("Đơn hàng ...
   đã được thanh toán") and a dong total has no decimals.
3. **Given** the settlement message is redelivered, **When** Order processes it again, **Then** no second email is
   requested (the guarded settle affects zero rows).
4. **Given** the `EmailRequested` message is redelivered, **When** Identity consumes it again, **Then** no second
   row is kept and no second email is sent.
5. **Given** an order that fails, **When** it settles `Failed`, **Then** no email is requested.

---

### US2 - A mail server that is down delays email, never loses it (Priority: P1)

**Why this priority**: An email that is lost silently is worse than none, because the shop believes it was sent.
And a checkout that waits on a mail server would let an outside service refuse orders.

**Independent Test**: Stop Mailpit, place an order: it settles `Paid` and its email waits as `Pending` with an
error. Start Mailpit: the email arrives once, by itself.

**Acceptance** (as first written)
1. With the mail server stopped, an order still settles `Paid`. The email is not part of what the order
   waits for.
2. When the mail server returns, the waiting email goes out, once, without anybody resending it.
3. An email that still cannot be sent after its retries is kept with its last error, so it can be seen.

**Acceptance Scenarios**:

1. **Given** the mail server is down, **When** an order settles, **Then** the order is `Paid` and its email is
   `Pending` with `Attempts` 1, a `LastError`, and a `NextAttemptAt` one minute later.
2. **Given** that email and a mail server back up, **When** the next attempt is due, **Then** it is sent once and
   marked `Sent`.
3. **Given** a mail server that never comes back, **When** 12 attempts have failed, **Then** the email is `Failed`
   with its last error and is not tried again.
4. **Given** an email for a person Identity does not know, or a template it has no words for, or data missing a
   value, **When** it is dispatched, **Then** it is `Failed` at once with the reason and never retried.

---

### US3 - Nothing leaves the machine in development (Priority: P1)

Mailpit runs in `docker-compose.yml`, and every email the stack sends is readable at
`http://localhost:8025`. No real inbox is ever reached from a development stack.

**Why this priority**: A development stack seeded with test accounts must not be able to write to a real person.

**Independent Test**: `docker compose up -d` starts `e-commerce-mailpit`; an order placed against the stack
appears in its inbox; the Identity container's `SMTP_HOST` is `mailpit`.

**Acceptance Scenarios**:

1. **Given** the compose stack, **When** Identity sends, **Then** the message goes to Mailpit's SMTP on 1025 and is
   readable at `http://localhost:8025`.
2. **Given** Mailpit is restarted, **When** its inbox is opened, **Then** earlier mail is still there (it keeps its
   mail on a volume).

---

### Edge Cases

- **Commit fails after a successful send.** That one email goes out twice - at-least-once, the accepted price (see
  "Why sending runs inside the claim transaction" in the plan).
- **Two Identity instances.** The claim uses `FOR UPDATE SKIP LOCKED`, so one row is sent by one instance.
- **An order with no stored language** (orders from before specs/021). The request's language is empty; Identity
  queues it as Vietnamese, the default.
- **A language with no words** (anything but `vi` and `en`). Vietnamese.
- **Identity running with no mail server at all** (the `auth-smoke` CI job). The dispatcher logs, pushes the email
  back and keeps running; nothing else is affected.
- **The recipient's name and address** are read by Identity when it sends, never copied into the request.

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-006**: When an order settles `Paid`, Order MUST request one `OrderPaid` email to the buyer, with the order
  id, total and currency, in the order's language, inside the settlement's transaction.
- **FR-007**: The development stack MUST deliver every email to a local mail catcher and to no real inbox.

### Key Entities

- **Email request** (`EmailRequested`): who, which template, the data and the language. No address and no
  sentence.
- **Outgoing email** (`outgoing_emails` in Identity): a kept request with its status (`Pending`, `Sent`,
  `Failed`), attempts, next attempt, last error and when it was sent.
- **Template**: a subject and a body per template and language, filled at send time with the recipient's first
  name, the data and a storefront link.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A paid order produces exactly one confirmation in Mailpit, and still one after further sweeps.
- **SC-002**: With the mail server stopped, 100% of orders still settle; their emails are delivered once when it
  returns, with no manual step.
- **SC-003**: An email that cannot be delivered is `Failed` with a reason after at most 12 attempts, never silently
  dropped.
- **SC-004**: No email from a development stack reaches anything but Mailpit.

## Assumptions

- The order's language (`orders.Language`, specs/021) is the right language for its confirmation; a stored
  per-person preference does not exist yet.
- Plain SMTP with no authentication and no TLS is enough for Mailpit; a real provider replaces the transport.
- The storefront's address for links comes from `STOREFRONT_URL` (default `http://localhost:8088`).

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
