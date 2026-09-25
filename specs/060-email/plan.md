# Implementation Plan: Email

**Branch**: `060-email` | **Spec**: [spec.md](spec.md) | **Issue**: #102

## Design

**Contract**
- `Ecommerce.Contracts/Identity/EmailRequested(EmailId, RecipientId, Template, Data, Language, RequestedAt)`.
  It is a request to Identity, so it lives with Identity's contracts.

**Shared**
- `Ecommerce.Shared/Email`: `IEmailSender` / `EmailSender`, which publishes; `EmailTemplate` constants;
  `AddEmailSender()`.
- This is the same shape as `INotifier`. A caller sends before its one save, or inside a `stage`.

**Identity**

*Domain.* `OutgoingEmail`, stored in `outgoing_emails`:

| Column | Notes |
| :-- | :-- |
| `Id` | the email id |
| `RecipientId`, `Template`, `DataJson`, `Language` | the request |
| `Status` | Pending / Sent / Failed, stored as text |
| `Attempts`, `NextAttemptAt` | for backoff |
| `SentAt`, `LastError`, `CreatedAt` | |

A partial index on `NextAttemptAt` covers pending rows.

*Application.*
- `IOutgoingEmailRepository`:
  - `QueueAsync`: `INSERT ... ON CONFLICT DO NOTHING`;
  - `ClaimDueAsync(now, batch)`: `FOR UPDATE SKIP LOCKED`, inside the dispatch transaction.
- `IEmailTransport`: `SendAsync(to, subject, text)`.
- `EmailTemplates`: `Render(template, language, data)` returns a subject and a body, or nothing for an
  unknown template.
- `DispatchEmailsCommand` does the claim → render → send → mark loop, with backoff.

*Infrastructure.*
- `SmtpEmailTransport`: `System.Net.Mail`, with `Email:Host`/`Port`/`From` overridden by `SMTP_HOST` and
  `SMTP_PORT`.
- `EmailDispatchSweeper`: a hosted service with a `PeriodicTimer` (`Email:SweepSeconds`, default 15).

*WebApi.* `QueueEmailConsumer` (a queue named for what it does) calls `QueueAsync`.

**Order**
- `OrderNoticeFacts` gains `Language`.
- `OrderNotices.PaidAsync` also sends `EmailTemplate.OrderPaid` to the buyer, in the order's language,
  inside the settle stage (specs/042's transaction).

**Compose and CI**
- `mailpit` (axllent/mailpit): SMTP on 1025, UI on 8025, in `docker-compose.yml`. The identity container
  gets `SMTP_HOST=mailpit`.
- CI needs nothing: tests use a fake transport, and auth-smoke still starts Identity with no mail server.
  The sweeper fails quietly and retries.

## Why sending runs inside the claim transaction

The row stays locked while the message is handed to SMTP, so two instances never send the same row. If
the commit fails after a successful send, that one email may go out twice. That is at-least-once, the
accepted price. The alternative, marking the row sent before sending, loses the email whenever the send
then fails.

## Constitution check

- III: the email request commits with the order's settlement, and queueing is idempotent on its id.
  Pass.
- I: no new synchronous call. Identity learns of an email only through the broker. Pass.
- V: tests first for queueing, dispatching, backoff, an unknown recipient and templates, plus an
  end-to-end run against Mailpit: an order, then the mail server stopped and restarted. Pass.
- Rollback: a new table only, so an older Identity ignores it.
