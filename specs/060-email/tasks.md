---
description: "Task list for Email"
---

# Tasks: Email

> Completed on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first (Identity `EmailTests`, Order `NotificationTests`), plus an end-to-end run
against Mailpit (constitution Principle V).

## Format: `[ID] [P?] [Story] Description`

The first seven tasks are the list as written on 2026-09-25, kept verbatim. T008 onwards break them down.

- [X] T001 Contract `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/EmailRequested.cs`; seam `server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs`
- [X] T002 [US1] [US2] Identity tests first in `server/tests/Ecommerce.Identity.Tests/EmailTests.cs`: queued once on redelivery; dispatched and marked Sent; the transport down leaves it Pending with a later attempt, and it is sent when back; an unknown recipient is Failed; attempts run out to Failed; the language picks the template; Vietnamese is the fallback
- [X] T003 [US1] [US2] Identity: `Domain/Entities/OutgoingEmail.cs`, configuration + migration `AddOutgoingEmails`, `Application/Email/` (repository interface, transport interface, templates, `DispatchEmailsCommand`), `Infrastructure/Email/` (repository, SMTP transport, sweeper), `WebApi/Consumers/QueueEmailConsumer.cs`, `Program.cs`
- [X] T004 [US1] Order: `OrderNoticeFacts.Language`; `OrderNotices.PaidAsync` sends the confirmation; test in `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`
- [X] T005 [US3] Mailpit in `server/docker-compose.yml`, `SMTP_HOST` for identity in `docker-compose.app.yml`, `.env.example`
- [X] T006 End to end against Mailpit: one confirmation in the order's language; the mail server stopped, the order settles, the mail server restarted, the email arrives once
- [X] T007 Mutation checks; docs: `docs/features/email.md` (new), `docs/README.md`, `docs/reference` regenerated, `docs/infrastructure/running-in-containers.md`, `docs/guides/getting-started.md`, `docs/project/*`, CLAUDE.md

---

## Phase 1: Setup and the seam

- [X] T008 [P] Create `EmailRequested(EmailId, RecipientId, Template, Data, Language, RequestedAt)` in `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/EmailRequested.cs`
- [X] T009 [P] Create `IEmailSender`, `EmailSender` (publishes with a v7 id), `EmailTemplate.OrderPaid` and `AddEmailSender()` in `server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs`
- [X] T010 [P] [US3] Add `mailpit` (`axllent/mailpit:v1.21`, 1025/8025, `MP_DATABASE` on the `mailpit_data` volume) to `server/docker-compose.yml`
- [X] T011 [P] [US3] Give the identity container `SMTP_HOST=mailpit`, `SMTP_PORT=1025`, `STOREFRONT_URL=http://localhost:8088` in `server/docker-compose.app.yml`; add `SMTP_HOST`, `SMTP_PORT`, `STOREFRONT_URL` to `server/.env.example`

## Phase 2: Foundational - Identity keeps requests

- [X] T012 Create `OutgoingEmail` and `OutgoingEmailStatus` (`Pending`, `Sent`, `Failed`) in `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/OutgoingEmail.cs`
- [X] T013 Create `OutgoingEmailConfiguration` (table `outgoing_emails`, `ValueGeneratedNever` id, jsonb data, status as string, partial index `IX_outgoing_emails_due`, index on `RecipientId`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/`, and the `OutgoingEmails` set on `ApplicationDbContext`
- [X] T014 Add migration `20260925032037_AddOutgoingEmails` under `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Migrations/`
- [X] T015 Declare `IOutgoingEmailRepository`, `IEmailTransport` and `EmailOptions` (`StorefrontUrl`, `MaxAttempts` 12, `Batch` 50) in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailInterfaces.cs`
- [X] T016 Implement `OutgoingEmailRepository` (`QueueAsync` with `ON CONFLICT DO NOTHING`, `ClaimDueAsync` with `FOR UPDATE SKIP LOCKED`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/OutgoingEmailRepository.cs`
- [X] T017 Implement `QueueEmailCommand` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/QueueEmailCommand.cs` and `QueueEmailConsumer` in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Consumers/QueueEmailConsumer.cs`; register it in `Program.cs` (Identity's first consumer)

## Phase 3: User Story 1 - a customer receives an order confirmation (P1)

- [X] T018 [US1] Write `EmailTemplates` (`OrderPaid` in `vi` and `en`, `vi` fallback, money in the reader's format with no decimals for VND, `null` for an unknown template or incomplete data) in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplates.cs`
- [X] T019 [US1] Add `Language` to `OrderNoticeFacts` and send `EmailTemplate.OrderPaid` from `OrderNotices.PaidAsync` in `server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/OrderNotices.cs`; read `orders.Language` into the facts in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs`
- [X] T020 [US1] Inject `IEmailSender` into `CompleteOrderCommandHandler` (`server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/CompleteOrder/CompleteOrderCommandHandler.cs`) and call `AddEmailSender()` in `server/src/Services/Order/Ecommerce.Order.WebApi/Program.cs`
- [X] T021 [P] [US1] Add `A_paid_order_asks_for_one_confirmation_email_in_its_language_and_a_failed_one_for_none` to `server/tests/Ecommerce.Order.Tests/NotificationTests.cs` (with the sender registered in `OrderTestFixture.cs`)

## Phase 4: User Story 2 - a mail server that is down delays email, never loses it (P1)

- [X] T022 [US2] Implement `DispatchEmailsCommandHandler` (claim, render, send, mark; backoff 1 min doubling to 1 h; `Failed` after 12; no recipient / no words fail at once) in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/DispatchEmailsCommand.cs`
- [X] T023 [US2] Implement `SmtpEmailTransport` and `SmtpOptions` (`Email:SmtpHost`, `Email:SmtpPort`, `Email:From`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/SmtpEmailTransport.cs`, with `SMTP_HOST`, `SMTP_PORT` and `STOREFRONT_URL` overrides in `DependencyInjection.cs`
- [X] T024 [US2] Implement `EmailDispatchSweeper` (`PeriodicTimer`, `Email:SweepSeconds` default 15, one bad tick never ends it) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/EmailDispatchSweeper.cs` and register it in `Program.cs`
- [X] T025 [P] [US2] The ten `EmailTests` in `server/tests/Ecommerce.Identity.Tests/EmailTests.cs`, with a fake transport and helpers in `IdentityTestFixture.cs`

## Phase 5: Polish

- [X] T026 Mutations: queueing without `ON CONFLICT` (red); a failed send gives up at once (2 red); a paid order asks for no email (red); each restored
- [X] T027 Identity 93/93 and Order 185/185 against real PostgreSQL; end to end against rebuilt Identity and Order containers and Mailpit (one English email; Mailpit stopped, order `Paid`, email `Pending` attempt 1; Mailpit back, Vietnamese email `Sent` attempt 2)
- [X] T028 [P] Regenerate `docs/reference/{data-model,messages}.md` with `python docs/tools/generate_reference.py`; add decision 44 to `docs/project/decisions.md`; update the pages listed in [plan.md](plan.md), including two stale known-limits in `docs/features/moderation-and-staff.md` left over from #127 and #128
- [X] T029 Merge through PR #143 (squash, 2026-09-25), closing #102

## Dependencies

T008-T009 before everything else. T012-T017 before US1 and US2. T018 before T022 (dispatch renders). T019-T021 are
Order-side and independent of Identity's dispatch. T026-T028 after both stories; T029 last.

## Notes

- 29 tasks; the seven original ones are the summary, T008-T028 their breakdown.
- No Bruno request: no endpoint was added.
