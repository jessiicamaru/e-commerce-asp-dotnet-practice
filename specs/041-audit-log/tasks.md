---
description: "Task list for the audit log"
---

# Tasks: An audit log of who did what

> Completed on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/](contracts/)

**Tests**: included - idempotence belongs to the database (Principle V), and "one entry per action" can
only be shown by running each action.

## Format: `[ID] [P?] [Story] Description`

T001-T012 are the tasks as written during the work, kept as they were. T013 onward break them down by the
files the merge actually touched, recorded afterwards; all were done in #93.

## Original tasks

- [X] T001 Contracts `AuditEntryRecorded`; Shared `Audit/*` (+ Contracts reference); redaction tests
- [X] T002 Activity service skeleton (4 projects, Program, health, db, migrations) + test project
- [X] T003 Tests first: record + diff + redaction + idempotence + filters + summary - tests/Ecommerce.Activity.Tests
- [X] T004 Activity: entity, config, `RecordAuditEntryCommand` (diff), queries, controller (Admin), consumer
- [X] T005 Instrument Identity; tests
- [X] T006 Instrument Catalog; tests
- [X] T007 Instrument Inventory; tests
- [X] T008 Instrument Order; tests
- [X] T009 Instrument Payment; tests
- [X] T010 Infra: compose, gateway, Dockerfile, CI, start-dev, env example, slnx
- [X] T011 Client: admin Audit log page + entry dialog; vi/en; tests
- [X] T012 Bruno: audit list (admin 200, customer 403); verify-saga; docs; mutation checks

> The Activity service's 24 tests were written with its code; the diff test found a real defect - a JSON
> null leaf crashed the comparison - before anything shipped. Per-service audit tests: Identity 3, Catalog 2,
> Inventory 2, Payment 1, Order 4, each asserting one entry per action, the actor, and none for a refused
> change. End to end: Bruno reads the order it followed back from `/api/audit` (placed, prepared, shipped,
> received - once each), 403 for a customer, 401 anonymous; verify-saga.sh passes; the admin page shows
> the diff old → new.
> Mutations, one at a time, each turning a test red: redaction off, the `ON CONFLICT` removed, a parcel move
> saved without its entry, a refused sign-in recorded under another name.

## Phase 1: Setup

- [X] T013 Add `server/src/BuildingBlocks/Ecommerce.Contracts/Activity/AuditEntryRecorded.cs`
- [X] T014 Add a project reference from `server/src/BuildingBlocks/Ecommerce.Shared/Ecommerce.Shared.csproj` to `Ecommerce.Contracts`
- [X] T015 Create the four projects under `server/src/Services/Activity/` and `server/tests/Ecommerce.Activity.Tests/`, and register them in `server/Ecommerce.slnx`
- [X] T016 [P] Add `postgres-activity` (5440, `ecommerce_activity_db`) to `server/docker-compose.yml` and the Activity app to `server/docker-compose.app.yml`
- [X] T017 [P] Add `ACTIVITY_DB_PORT=5440` to `server/.env.example`; add Activity to `server/start-dev.ps1` and `server/start-dev.sh`
- [X] T018 [P] Add the Activity project line to `server/Dockerfile`
- [X] T019 [P] Add `audit-route`, `audit-root-route`, `activity-health-route` and `activity-cluster` to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`

## Phase 2: Foundational

- [X] T020 Write `AuditCategory`, `AuditActor`, `IAuditTrail`, `AuditTrail`, `AuditSnapshot` (redaction) and `AddAuditTrail` in `server/src/BuildingBlocks/Ecommerce.Shared/Audit/AuditTrail.cs`
- [X] T021 [P] Tests for redaction at any depth and by name in `server/tests/Ecommerce.Activity.Tests/RedactionTests.cs`
- [X] T022 [P] Tests for the actor's role ranking and the system actor in `server/tests/Ecommerce.Activity.Tests/AuditTrailTests.cs`
- [X] T023 `AuditEntry` in `server/src/Services/Activity/Ecommerce.Activity.Domain/Entities/AuditEntry.cs`; `ActivityDbContext` with `AddTransactionalOutboxEntities()`; `AuditEntryConfiguration` with the four indexes
- [X] T024 Migration `20260923192252_InitialCreate` in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Migrations/`
- [X] T025 `server/src/Services/Activity/Ecommerce.Activity.WebApi/Program.cs`: port 5063, JWT, MassTransit with the `ActivitySvc` prefix, EF outbox/inbox on consumer endpoints, `/health` with the database check

## Phase 3: User Story 2 - Everything that matters is recorded, once (P1)

- [X] T026 [US2] Tests for the diff (only changed fields, paths, creation/deletion, type change, JSON null, the 200 cut) in `server/tests/Ecommerce.Activity.Tests/AuditDiffTests.cs`
- [X] T027 [US2] `AuditDiff` in `server/src/Services/Activity/Ecommerce.Activity.Application/Audit/AuditDiff.cs`
- [X] T028 [US2] `RecordAuditEntryCommand` and `AuditRepository.TryAddAsync` (`INSERT ... ON CONFLICT ("Id") DO NOTHING`) in `.../Application/Audit/Commands/RecordAuditEntryCommand.cs` and `.../Infrastructure/Persistence/Repositories/AuditRepository.cs`
- [X] T029 [US2] `RecordAuditEntryConsumer` in `.../WebApi/Consumers/RecordAuditEntryConsumer.cs`
- [X] T030 [P] [US2] Identity: `LoginCommandHandler` (signed in, refused - saved on its own), `RegisterCommandHandler`, `RegisterSellerCommand`, `SellerCommands` (rename), the three address handlers; `Common/AuditActors.cs`; `server/tests/Ecommerce.Identity.Tests/AuditTests.cs`
- [X] T031 [P] [US2] Catalog: category create/delete, product create/delete, prices, translations, variants, product and variant images, `OrphanImages`; `Common/CatalogAudit.cs`; `server/tests/Ecommerce.Catalog.Tests/AuditTests.cs`
- [X] T032 [P] [US2] Inventory: `SetStockOnHandCommandHandler`, `ExpireStockCommandHandler`, `RestockCancelledOrderCommandHandler`; `server/tests/Ecommerce.Inventory.Tests/AuditTests.cs`
- [X] T033 [US2] Order: `stage` parameters on `IOrderRepository`/`IPayoutRepository` and `OrderRepository`/`PayoutRepository`; `SubmitOrderCommandHandler`, `CancelOrderCommands`, `DeliveryCommands`, `FulfilmentStep`, `SellerFulfilmentCommands`, `RecordPayoutCommand`, `Orders/Common/ParcelAudit.cs`; `server/tests/Ecommerce.Order.Tests/AuditTests.cs`
- [X] T034 [P] [US2] Payment: `ChargeOrderCommandHandler`, `RefundOrderCommandHandler`; `server/tests/Ecommerce.Payment.Tests/AuditTests.cs`; test fixtures in every instrumented service register the audit trail
- [X] T035 [US2] Call `AddAuditTrail("<service>")` in each instrumented service's `Program.cs`

## Phase 4: User Story 1 - An administrator reads what happened (P1)

- [X] T036 [US1] Queries (list with validator, one, summary) in `.../Application/Audit/Queries/AuditQueries.cs`; responses in `.../Application/Audit/AuditResponses.cs`
- [X] T037 [US1] `AuditController` (`[Authorize(Roles = "Admin")]`) in `.../WebApi/Controllers/AuditController.cs`
- [X] T038 [US1] Tests for filters, summary, 404 and an unknown category in `server/tests/Ecommerce.Activity.Tests/AuditLogTests.cs`
- [X] T039 [P] [US1] Client service and types in `client/src/services/audit/`, hooks in `client/src/hooks/audit/index.ts`, query keys
- [X] T040 [US1] Page `client/src/pages/admin-audit/index.tsx` (category tabs with counts, actor and period filters in the URL), `entry-dialog.tsx` (changes old → new), `format.ts`; route and admin layout link; vi/en strings
- [X] T041 [P] [US1] Client tests `client/src/pages/admin-audit/index.test.tsx`, `format.test.ts`, `client/src/services/audit/index.test.ts`

## Phase 5: Polish

- [X] T042 [P] Bruno `bruno/admin-audit/` (the order it followed, the summary) and `bruno/security-checks/` (403 customer, 401 anonymous)
- [X] T043 CI: Activity test database and the image/publish lists in `.github/workflows/ci.yml` (the registry check now expects nine server images)
- [X] T044 [P] CLAUDE.md: the audit paragraph, including the product-image exception
- [X] T045 Mutation checks: redaction, `ON CONFLICT`, a parcel move without its entry, the refused sign-in entry - each turned a test red
- [X] T046 Run `verify-saga.sh`, the Bruno collection (125/125) and every existing suite; merged as PR #93 (closes #86)
