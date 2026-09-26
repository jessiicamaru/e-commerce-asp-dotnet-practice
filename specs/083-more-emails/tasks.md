---
description: "Task list for Emails for what happens to people"
---

# Tasks: Emails for what happens to people

> Completed on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Input**: Design documents from `/specs/083-more-emails/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. "A change that did not happen asks for nothing" is an atomicity property (Principle III) and is
tested by reading what the harness saw published.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 order emails, US2 lock and ban, US3 back in stock, US4 the reader's language

The original five tasks are kept in full: original T001 is T001, T002 is T002-T004, T003 is T005-T007, T004 is
T008-T009, T005 is T010-T012.

---

## Phase 1: Foundational

- [X] T001 Eight template names in `EmailTemplate` (Shared, `server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs`), plus `ReadersLanguage`. In Identity's `EmailTemplates` (`server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplates.cs`): words in both languages, placeholders, required placeholders, sample data, values, and the console's order.

## Phase 2: User Story 4 - the reader's language (P4, but blocking US2 and US3)

- [X] T002 [US4] `users.Language` in `.../Identity.Domain/Entities/User.cs` (max length 8 in `.../Infrastructure/Configurations/UserConfigurations.cs`) and migration `.../Infrastructure/Migrations/20260926161716_AddUserLanguage.cs` (it only adds a column)
- [X] T003 [US4] It is recorded at sign-up, sign-in and renewal: `RegisterCommandHandler`, `RegisterSellerCommand`, `LoginCommand(Handler)`, `RefreshTokenCommand(Handler)` under `.../Identity.Application/Auth/Commands/`; `RecordLanguageAsync`, one guarded `UPDATE`, in `.../Common/Interfaces/IUserRepository.cs` and `.../Infrastructure/Persistence/Repositories/UserRepository.cs`; `AuthController` passes `RequestLanguage()` to login and refresh
- [X] T004 [US4] `QueueEmailCommand` resolves `ReadersLanguage` in `.../Identity.Application/Email/QueueEmailCommand.cs`

## Phase 3: Senders

- [X] T005 [US1] Order: parcel shipped, whether the seller or the shop ships it (`OrderNotices.ShippedAsync`, `ParcelAudit.RecordMoveAsync`, `FulfilmentStep`, `ShipOrderCommandHandler`, `SellerFulfilmentCommands`); a cancellation, by the customer or by staff (`CancelOrderCommands`, `OrderNotices.CancelledAsync`); a return accepted, refused or refunded (`Returns/ReturnFeatures.cs`, with `ReturnParcel.Language` filled by `ReturnRepository`)
- [X] T006 [P] [US3] Catalog: back in stock, with `AddEmailSender`, in `.../Catalog.Application/Products/Availability/RecordStockAvailabilityCommandHandler.cs` and `.../Catalog.WebApi/Program.cs`
- [X] T007 [P] [US2] Identity: a lock or a ban, with `AddEmailSender`, in `.../Identity.Application/Users/UserAdministration.cs` and `.../Identity.WebApi/Program.cs`

## Phase 4: Tests

- [X] T008 Server tests:
  - every template has words and renders its sample whole;
  - the shipped and lock wording;
  - lock and ban ask for one email each, and a refused lock asks for none;
  - the language is learnt, and nonsense changes nothing;
  - the reader's language is resolved;
  - shipping and cancelling ask for one email each in the order's language;
  - return decisions and the refund;
  - back in stock asks for one email per saver, and nothing for a product off the shelf.

  In `server/tests/Ecommerce.Identity.Tests/AccountEmailTests.cs` (new, 7), `EmailTemplateTests.cs`,
  `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`, `ReturnTests.cs`,
  `server/tests/Ecommerce.Catalog.Tests/SavedProductTests.cs`, with `AddEmailSender` in the fixtures
- [X] T009 [P] Storefront: every template is named in both languages - `client/src/locales/{en,vi}/admin.json`, `client/src/pages/admin-emails/index.test.tsx`

## Phase 5: Polish and verification

- [X] T010 Fix the flaky insight test found on the first CI run: `server/tests/Ecommerce.Order.Tests/InsightDays.cs` hands out days ten apart; `InsightsTests.cs` and `SellerInsightsTests.cs` use it
- [X] T011 Mutations, Bruno and Mailpit against the rebuilt stack (`bruno/admin-audit/ship order.yml` reads the email back; `bruno/admin-users/an administrator lists the emails.yml` asserts 22 entries), and the docs: email feature page, moderation page, CLAUDE.md, `docs/reference/data-model.md`, counts, timeline and backlog.
- [X] T012 Merged as #171 on 2026-09-26 (closes #167), after Identity 170/170, Order 265/265, Catalog 205/205, client 457/457, and Bruno 267/267 requests and 437/437 tests

---

## Dependencies & Execution Order

- T001 blocks everything.
- US4 (T002-T004) blocks US2 and US3, whose emails ask for `ReadersLanguage`; US1 does not need it.
- T005, T006 and T007 are three services and could run in parallel once T001 and T004 exist.
- T008 covers all of them; T010 was forced by CI, not planned.
