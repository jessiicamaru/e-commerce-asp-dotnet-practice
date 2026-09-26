# Phase 0 Research: Email

> Written on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-25

Six decisions. D1 to D3 were recorded in the pull request as "decided on the user's behalf"; the others are
reconstructed from the code. The project-level record is decision 44 in
[docs/project/decisions.md](../../docs/project/decisions.md).

---

## D1 - Identity sends, not Activity

**Decision**: Every email request goes to Identity, which keeps and sends it. The issue suggested Activity.

**Rationale**: Identity is the one service that knows email addresses. Sending from Activity would need a read
model of every address, fed by events, plus a backfill for every account created before it - and an address
change would reach it seconds late. The next two users of the seam (#103 password reset, #106 address
confirmation) are Identity features anyway.

**Alternatives considered**:

- **Activity sends** (as #102 suggested). Rejected for the copy of every address above.
- **Each service sends its own email.** Rejected: every service would need addresses and SMTP settings, and a
  second copy of the retry logic.
- **Order includes the address in the request.** Rejected: Order does not have it, and copying addresses into
  messages spreads personal data into every queue and outbox row.

---

## D2 - A durable table and a sweeper, not a retry policy on the consumer

**Decision**: `QueueEmailConsumer` only stores the request. `EmailDispatchSweeper` (every `Email:SweepSeconds`, 15)
claims what is due and sends it; a failure pushes `NextAttemptAt` back 1, 2, 4 ... minutes up to an hour; after
`Email:MaxAttempts` (12) the row is `Failed` with its last error.

**Rationale**: An in-memory retry gives up within minutes, forgets everything on restart, and leaves the message in
an error queue nobody reads. A row with its own next-attempt time survives both a restart and a mail server that
is down for an hour, and its `LastError` is visible. It is the codebase's sweeper shape (Inventory's expiry
sweeper, the saga's payment-timeout sweeper of specs/053).

**Alternatives considered**:

- **MassTransit retry/redelivery on the consumer.** Rejected for the reasons above; redelivery would also need the
  delayed-message plugin that compose and CI do not have (the same reason as specs/053).
- **Send from the consumer, store only on failure.** Rejected: it ties the broker's delivery to the mail server,
  and a mail server that is down fills the error queue.

---

## D3 - At-least-once: the send runs inside the claim's transaction

**Decision**: `DispatchEmailsCommandHandler` claims a batch with `FOR UPDATE SKIP LOCKED` inside
`IUnitOfWork.ExecuteInTransactionAsync`, sends each, marks it, and saves once at the end.

**Rationale**: The lock is what keeps two instances from sending one row. If the commit fails after a successful
send, that one email goes out twice - the accepted price. Marking the row sent before sending would lose the email
whenever the send then failed.

**Alternatives considered**:

- **Mark `Sent` in one transaction, send after.** Rejected: at-most-once loses email on any SMTP failure.
- **A `Sending` state with a lease.** Rejected as more machinery than one duplicate email per failed commit is
  worth.

---

## D4 - The request carries a template and data, never a sentence

**Decision**: `EmailRequested(EmailId, RecipientId, Template, Data, Language, RequestedAt)`; Identity renders it at
send time with `EmailTemplates.Render`, reading the recipient's first name and address from `users`.

**Rationale**: The same rule as notices (specs/042): words live in one place and can change without touching
publishers. A rendered sentence would also carry the recipient's name through the broker.

**Alternatives considered**: render in the publishing service - rejected: every publisher would need the
recipient's name and the templates.

---

## D5 - The order's language, Vietnamese as the fallback; incomplete means `Failed`

**Decision**: The confirmation is written in `orders.Language` (carried on `OrderNoticeFacts.Language`). An empty
language is queued as `vi`; a language with no words renders in `vi`. An unknown template or data missing a value
renders as nothing and the row is `Failed` at once, as is a recipient Identity does not know.

**Rationale**: An order keeps its words in the language it was placed in (specs/021), so its email should match.
Vietnamese is the shop's default for its own text. An email with a hole in it is worse than one never sent, and
retrying cannot supply a missing template or person.

**Alternatives considered**: a per-person language preference - out of scope here; it arrived later as
`users.Language` (specs/083).

---

## D6 - Mailpit in compose, on a volume; plain SMTP

**Decision**: `axllent/mailpit:v1.21` in `docker-compose.yml`, SMTP on 1025, UI on 8025, `MP_DATABASE` on the
`mailpit_data` volume. `SmtpEmailTransport` uses `System.Net.Mail.SmtpClient` with no authentication and no TLS;
`SMTP_HOST` / `SMTP_PORT` point it elsewhere, and the Identity container gets `SMTP_HOST=mailpit`.

**Rationale**: A development stack must never reach a real inbox. During the end-to-end run, stopping Mailpit also
lost its in-memory inbox, which is why it now keeps its mail on a volume. The transport is a seam, like
`StubPaymentGateway`: a real provider replaces the class.

**Alternatives considered**: a file-drop transport in development - rejected: it would not exercise SMTP, and the
stopped-server scenario (US2) could not be run.
