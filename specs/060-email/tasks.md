# Tasks: Email

- [X] T001 Contract `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/EmailRequested.cs`; seam `server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs`
- [X] T002 [US1] [US2] Identity tests first in `server/tests/Ecommerce.Identity.Tests/EmailTests.cs`: queued once on redelivery; dispatched and marked Sent; the transport down leaves it Pending with a later attempt, and it is sent when back; an unknown recipient is Failed; attempts run out to Failed; the language picks the template; Vietnamese is the fallback
- [X] T003 [US1] [US2] Identity: `Domain/Entities/OutgoingEmail.cs`, configuration + migration `AddOutgoingEmails`, `Application/Email/` (repository interface, transport interface, templates, `DispatchEmailsCommand`), `Infrastructure/Email/` (repository, SMTP transport, sweeper), `WebApi/Consumers/QueueEmailConsumer.cs`, `Program.cs`
- [X] T004 [US1] Order: `OrderNoticeFacts.Language`; `OrderNotices.PaidAsync` sends the confirmation; test in `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`
- [X] T005 [US3] Mailpit in `server/docker-compose.yml`, `SMTP_HOST` for identity in `docker-compose.app.yml`, `.env.example`
- [X] T006 End to end against Mailpit: one confirmation in the order's language; the mail server stopped, the order settles, the mail server restarted, the email arrives once
- [X] T007 Mutation checks; docs: `docs/features/email.md` (new), `docs/README.md`, `docs/reference` regenerated, `docs/infrastructure/running-in-containers.md`, `docs/guides/getting-started.md`, `docs/project/*`, CLAUDE.md
