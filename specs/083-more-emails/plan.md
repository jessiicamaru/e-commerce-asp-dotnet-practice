# Implementation Plan: Emails for what happens to people

> Written on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Branch**: `083-more-emails` | **Date**: 2026-09-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/083-more-emails/spec.md`

## Summary

Add eight templates to the email machinery of specs/060 and 077, and ask for each where its notice is already
sent. The request travels as the existing `EmailRequested` message through each sender's outbox, so an email
commits with the change it describes: Order asks for the shipped, cancelled and three return emails in the order's
language; Catalog asks for back in stock and Identity for a lock or ban, both in `EmailTemplate.ReadersLanguage`.
Identity resolves that when it queues the email, from a new nullable `users.Language` it records from
`Accept-Language` at sign-up, sign-in and session renewal. One additive migration.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript in the storefront (template labels only)

**Primary Dependencies**: MassTransit 8.3.6 (the EF outbox of each sender), MediatR 12.4.1, EF Core with Npgsql,
`Ecommerce.Shared/Email` (`IEmailSender`, `EmailTemplate`)

**Storage**: PostgreSQL 16, `ecommerce_identity_db` (5435): `users.Language` added; `outgoing_emails` unchanged

**Testing**: xUnit against real PostgreSQL with the MassTransit harness (`Ecommerce.Identity.Tests`,
`Ecommerce.Order.Tests`, `Ecommerce.Catalog.Tests`); Vitest; Bruno with Mailpit

**Target Platform**: Identity (5056), Order (5059), Catalog (5057), the storefront's `/admin/emails`

**Project Type**: Changes in three services, one shared building block and the client

**Performance Goals**: None stated. Session renewal gains one guarded `UPDATE` that writes nothing when the
language is unchanged

**Constraints**: An email must commit with its change or not at all (Principle III); a change that did not happen
must ask for nothing; the reader's language must be known to Identity only

**Scale/Scope**: 8 templates (11 in all), 2 languages, 22 editable entries

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity alone knows addresses and now languages; Order and Catalog send ids and data, never an address. Catalog does not learn a saver's language - it asks Identity to fill it in (`ReadersLanguage`). No service reads another's database |
| **II. Clean Architecture Layering** | **Pass.** Handlers depend on `IEmailSender` (Shared abstraction over `IPublishEndpoint`); `RecordLanguageAsync` is declared in Application's `IUserRepository` and implemented in Infrastructure; controllers only pass `RequestLanguage()` into the command |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Every `SendAsync` is called before the handler's one save, or inside the repository's `stage` callback of a guarded move (ship, cancel, return steps), so the email request commits with the change. Identity's consumer keeps each request once by `EmailId` (unchanged). The language write is a guarded `UPDATE ... WHERE "Language" IS NULL OR "Language" <> @l` |
| **IV. Identity Comes From the Token** | **Pass.** Recipients are the order's buyer, the saver, the moderated user - never a request value. The language comes from a header, but it is a preference about the caller's own account, recorded only by the caller's own sign-in or renewal |
| **V. Evidence Over Assumption** | **Pass.** Bruno reads the shipped email back from Mailpit against the rebuilt stack; the tests read what was published through the harness against real databases; four mutations were run. A flaky insight test found on the first CI run was diagnosed and fixed rather than re-run |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/083-more-emails/
├── spec.md
├── plan.md              # This file
├── research.md          # Seven decisions
├── data-model.md        # users.Language
├── quickstart.md
├── contracts/
│   ├── messages.md      # EmailRequested: new templates, ReadersLanguage
│   └── http-api.md      # Accept-Language on sign-up, sign-in, refresh; the templates list
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs        # 8 template names, ReadersLanguage
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/User.cs                          # Language
├── Ecommerce.Identity.Application/
│   ├── Email/EmailTemplates.cs                                        # words, placeholders, samples, values, order
│   ├── Email/QueueEmailCommand.cs                                     # resolves ReadersLanguage
│   ├── Auth/Commands/{Login,Refresh,Register,RegisterSeller}/...      # record the language
│   ├── Common/Interfaces/IUserRepository.cs                          # RecordLanguageAsync
│   └── Users/UserAdministration.cs                                    # lock and ban emails
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/UserConfigurations.cs                          # max length 8
│   ├── Persistence/Repositories/UserRepository.cs                    # the guarded UPDATE
│   └── Migrations/20260926161716_AddUserLanguage.cs
└── Ecommerce.Identity.WebApi/{Controllers/AuthController.cs, Program.cs}   # Accept-Language; AddEmailSender
server/src/Services/Order/Ecommerce.Order.Application/
├── Orders/Common/{OrderNotices,ParcelAudit}.cs                        # shipped and cancelled emails
├── Orders/Commands/{CancelOrder,Fulfilment,SellerFulfilment}/...       # pass IEmailSender through
└── Returns/ReturnFeatures.cs                                          # three return emails; ReturnParcel.Language
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/ReturnRepository.cs
server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Availability/RecordStockAvailabilityCommandHandler.cs
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs                 # AddEmailSender
server/tests/Ecommerce.Identity.Tests/AccountEmailTests.cs (new, 7), EmailTemplateTests.cs, IdentityTestFixture.cs
server/tests/Ecommerce.Order.Tests/{NotificationTests,ReturnTests}.cs, InsightDays.cs (new)
server/tests/Ecommerce.Catalog.Tests/{SavedProductTests,CatalogTestFixture}.cs
client/src/locales/{en,vi}/admin.json, client/src/pages/admin-emails/index.test.tsx
bruno/admin-audit/ship order.yml, bruno/admin-users/an administrator lists the emails.yml
```

Docs in the same change: `docs/features/email.md`, `docs/features/moderation-and-staff.md`, `CLAUDE.md`,
`docs/reference/data-model.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`,
`docs/project/timeline.md`, `docs/project/backlog.md`.

**Structure Decision**: every email is asked for next to the notice for the same event (`OrderNotices`,
`ReturnHandlers`, `UserAdministrationHandlers`, `RecordStockAvailabilityCommandHandler`), so the two cannot drift
apart and both share the event's transaction.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- Sellers are not emailed; the console and the bell tell them.
- Somebody who never signs in again keeps the language they last used.
- The back-in-stock email names the product in its default language, as the notice does.
