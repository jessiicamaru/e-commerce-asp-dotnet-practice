# Implementation Plan: Email

> Completed on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md).

**Branch**: `060-email` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md) | **Issue**: #102

## Summary

Any service asks for an email the way it asks for a notice: `IEmailSender` publishes `EmailRequested` through
the caller's outbox. Identity, the one service that knows addresses, keeps each request once in a new
`outgoing_emails` table and a hosted sweeper sends what is due over SMTP, with backoff and a terminal `Failed`.
Mailpit catches everything in development. The first email is the order confirmation, requested inside the
settlement's transaction. Decisions are in [research.md](research.md).

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
  `SMTP_PORT`. *Correction (2026-09-27):* the keys the code binds are `Email:SmtpHost`, `Email:SmtpPort` and
  `Email:From` (`SmtpOptions`); `SMTP_HOST` / `SMTP_PORT` override the first two. A third variable not planned
  here, `STOREFRONT_URL`, overrides `Email:StorefrontUrl`, the base of the link in an email.
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

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (EF Core outbox; Identity's first consumer), MediatR 12.4.1,
`System.Net.Mail.SmtpClient`, Mailpit `axllent/mailpit:v1.21` in compose

**Storage**: New table `outgoing_emails` in `ecommerce_identity_db` (5435), migration
`20260925032037_AddOutgoingEmails`

**Testing**: xUnit against real PostgreSQL with a fake `IEmailTransport` (`EmailTests`, 10 new); Order's
`NotificationTests` with MassTransit's harness; an end-to-end run against rebuilt Identity and Order containers
and Mailpit

**Target Platform**: Identity (5056) sends; Order (5059) asks; Mailpit SMTP 1025, UI 8025

**Project Type**: A new capability in Identity, a new seam in `Ecommerce.Shared`, one new contract

**Performance Goals**: None stated. One sweep every 15 seconds, at most 50 emails per sweep (`Email:Batch`)

**Constraints**: An email request commits with its change (Principle III); a checkout never waits on a mail
server; at-least-once delivery; nothing leaves a development machine

**Scale/Scope**: One template (`OrderPaid`) in two languages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md).

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No new synchronous call: Identity learns of an email only through the broker. Addresses stay in Identity, the one owner; Order sends a recipient id, never an address |
| **II. Clean Architecture Layering** | **Pass.** `OutgoingEmail` in Domain; repository and transport interfaces, templates and the dispatch command in Application; the SQL repository, SMTP transport and sweeper in Infrastructure; the consumer in WebApi |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The email request commits with the order's settlement, inside the settle stage. Queueing is idempotent on its id (`INSERT ... ON CONFLICT DO NOTHING`); the claim is `FOR UPDATE SKIP LOCKED` inside the dispatch transaction. The one duplicate possible is a send whose commit then fails, accepted and recorded |
| **IV. Identity Comes From the Token** | **Pass.** No endpoint was added. The recipient is the order's buyer from the row; Identity reads their address and name from its own `users` |
| **V. Evidence Over Assumption** | **Pass.** Tests first for queueing, dispatching, backoff, an unknown recipient and templates, plus an end-to-end run against Mailpit: an order, then the mail server stopped and restarted. Three mutations each turned a test red (PR #143) |

**Post-design re-check**: no violations. Rollback: a new table only, so an older Identity ignores it.

## Constitution check

- III: the email request commits with the order's settlement, and queueing is idempotent on its id.
  Pass.
- I: no new synchronous call. Identity learns of an email only through the broker. Pass.
- V: tests first for queueing, dispatching, backoff, an unknown recipient and templates, plus an
  end-to-end run against Mailpit: an order, then the mail server stopped and restarted. Pass.
- Rollback: a new table only, so an older Identity ignores it.

(The lines above are the check as first written; the table extends it to all five principles.)

## Project Structure

### Documentation (this feature)

```text
specs/060-email/
├── spec.md
├── plan.md                  # This file
├── research.md              # Six decisions
├── data-model.md            # outgoing_emails and an email's states
├── quickstart.md
├── contracts/
│   └── messages.md          # EmailRequested
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #143)

```text
server/src/BuildingBlocks/
├── Ecommerce.Contracts/Identity/EmailRequested.cs
└── Ecommerce.Shared/Email/EmailSender.cs                   # IEmailSender, EmailTemplate, AddEmailSender
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/OutgoingEmail.cs
├── Ecommerce.Identity.Application/Email/
│   ├── DispatchEmailsCommand.cs
│   ├── EmailInterfaces.cs                                  # IOutgoingEmailRepository, IEmailTransport, EmailOptions
│   ├── EmailTemplates.cs
│   └── QueueEmailCommand.cs
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/OutgoingEmailConfiguration.cs
│   ├── Email/{EmailDispatchSweeper,OutgoingEmailRepository,SmtpEmailTransport}.cs
│   ├── Migrations/20260925032037_AddOutgoingEmails.cs
│   ├── Persistence/ApplicationDbContext.cs
│   └── DependencyInjection.cs
└── Ecommerce.Identity.WebApi/{Consumers/QueueEmailConsumer.cs,Program.cs}
server/src/Services/Order/
├── Ecommerce.Order.Application/Orders/Commands/CompleteOrder/CompleteOrderCommandHandler.cs
├── Ecommerce.Order.Application/Orders/Common/OrderNotices.cs        # OrderNoticeFacts.Language, PaidAsync
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs
└── Ecommerce.Order.WebApi/Program.cs                                 # AddEmailSender
server/tests/Ecommerce.Identity.Tests/{EmailTests.cs,IdentityTestFixture.cs}
server/tests/Ecommerce.Order.Tests/{NotificationTests.cs,OrderTestFixture.cs}
server/docker-compose.yml, server/docker-compose.app.yml, server/.env.example
```

Documentation touched in the same change: `docs/features/email.md` (new), `CLAUDE.md`, `docs/README.md`,
`docs/features/{audit-and-notifications,auth/db-design,fulfilment-and-delivery,moderation-and-staff,shopping-and-checkout}.md`,
`docs/guides/getting-started.md`, `docs/infrastructure/running-in-containers.md`, `docs/overview/project-overview.md`,
`docs/project/{backlog,decisions,timeline}.md`, `docs/reference/{data-model,messages}.md` (regenerated),
`docs/testing/testing-strategy.md`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Nobody is alerted when an email fails.** It waits in `outgoing_emails` as `Failed`; a screen to see it came in
  [specs/087](../087-email-delivery/).
- Plain text only, words fixed in code; administrators edit them from [specs/077](../077-email-templates/).
- One template. The reset (061) and confirmation (063) emails, and later ones (083), reuse this seam.
- **Deployment coupling**: `EmailRequested` is a new contract, so Order and Identity must be deployed together
  (PR #143, "Worth knowing").
