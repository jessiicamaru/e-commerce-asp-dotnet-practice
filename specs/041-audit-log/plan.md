# Implementation Plan: An audit log of who did what

> Completed on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Branch**: `041-audit-log` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | [research](research.md)

## Summary

Every service that changes something a person must answer for publishes `AuditEntryRecorded` through its
own transactional outbox, before its single `SaveChangesAsync`, through a shared `IAuditTrail`. A new
ninth service, **Activity**, consumes it, computes the field-level diff once, and keeps it with
`INSERT ... ON CONFLICT ("Id") DO NOTHING` on the publisher's entry id. Administrators read the log
through three `Admin`-only endpoints and a new page in the admin console. Where a change is a guarded
statement in its own transaction (parcel moves, deliveries, payouts, cancellation) the repository takes a
`stage` callback so the entry is saved inside that transaction, by the request that won the guard only.

## Technical Context

**Language/Version**: C# / .NET 10.0; the storefront in React 19 + TypeScript

**Primary Dependencies**: MassTransit (RabbitMQ, EF Core outbox and inbox), MediatR, FluentValidation,
Npgsql EF Core, `System.Text.Json` (`JsonNode`) for snapshots and the diff; TanStack Query and shadcn/ui
in the client

**Storage**: PostgreSQL 16, new database `ecommerce_activity_db`, host port 5440; `jsonb` for snapshots
and the diff

**Testing**: xUnit against a real PostgreSQL (the idempotence under test is the database's primary key);
MassTransit's test harness in each instrumented service to read what was published; Vitest in the
client; Bruno through the gateway; `verify-saga.sh`

**Target Platform**: Linux container / Windows dev host, REST on 5063

**Project Type**: a new backend microservice (Domain / Application / Infrastructure / WebApi) plus
instrumentation of five existing services and one client page

**Performance Goals**: not set. The log is read by staff, a page at a time

**Constraints**: an entry commits with its change or not at all (Principle III); a redelivery leaves one
row; no secret ever leaves the recording service

**Scale/Scope**: one row per recorded action; indexes for the four ways the log is read (newest first, by
category, by actor, by subject). No retention policy (out of scope)

- **New service `Activity`** (Domain / Application / Infrastructure / WebApi, tests `Ecommerce.Activity.Tests`
  on 5440): `audit_entries` (id PK, category, action, actor id/email/role, subject type/id, summary,
  before/after `jsonb`, changes `jsonb`, service, occurred/recorded at; indexes on occurred, category,
  actor, subject). Consumer `RecordAuditEntryConsumer` → `RecordAuditEntryCommand`. Queries per D6.
- **Contracts**: `Activity/AuditEntryRecorded`.
- **Shared**: `Audit/IAuditTrail`, `AuditTrail`, `AuditCategory`, `AddAuditTrail(serviceName)`, snapshot +
  redaction.
- **Instrumented**: Identity (sign-in, refused sign-in, register, register-seller, shop rename, addresses),
  Catalog (product create/update/delete, prices, variants, images, categories, orphan reclaim), Inventory
  (stock set, expiry sweep), Order (placed, cancelled, parcel moves, received, auto-deliver, payout),
  Payment (charged/refused, refund).
- **Infra**: compose (db + app), gateway routes `/api/audit/**` and `/api/activity/health`, Dockerfile,
  CI (test DB, image list, publish lists), start-dev scripts, `.env.example`, slnx.
- **Client**: admin **Audit log** page (category tabs, filters, table) and entry dialog with the diff.

Two additions beyond that list, found in the merged code: Inventory also records `StockReturned` when a
cancelled order's units go back (specs/039's consumer), and the audit table carries `ChangeCount` beside
`Changes` so the list can show "3 changes" without reading the `jsonb`.

## Constitution Check

*GATE: evaluated before research and re-checked after design.* Against
[constitution.md](../../.specify/memory/constitution.md).

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Activity owns its data; others publish, never read it. It owns no business fact - an entry is a record *about* another service's change, and nothing decides anything from it. The only new shared code is one record in `Ecommerce.Contracts` and the `Audit` folder in `Ecommerce.Shared` (which now references `Ecommerce.Contracts`, pure records). |
| II - Clean Architecture | **Pass.** Consumers in WebApi, commands in Application, SQL in Infrastructure. `IAuditTrail` is an Application-facing abstraction over `IPublishEndpoint` (MassTransit.Abstractions); the diff is pure code in Application; the one hand-written `INSERT` lives in `AuditRepository`. |
| III - Atomic, idempotent | **Pass, and the reason for the design.** Published before the one save (outbox); consumed idempotently by entry id - the primary key plus `ON CONFLICT DO NOTHING`, a database guarantee, not the inbox alone. Guarded statements in their own transaction take a `stage` callback so the entry joins that transaction. The one exception (product images, a guarded `UPDATE` of its own since specs/019) is recorded, not hidden. |
| IV - Identity from the token | **Pass.** Actor from `ICurrentUser`, never a body. The only override is `AuditActor`, passed by server code where nobody is signed in yet (sign-in, registration) - never by a caller. Reads are `[Authorize(Roles = "Admin")]`. |
| V - Evidence | **Pass.** Tests: diff, redaction, idempotence, filters, admin-only; per-service tests that an action records one entry; verify-saga. Idempotence runs against a real PostgreSQL. Four mutations each turned a test red (see [tasks.md](tasks.md)). |

**Post-design re-check**: no violation. The two rows in Complexity Tracking are additions that needed a
reason, not breaches of a principle.

## Project Structure

### Documentation (this feature)

```text
specs/041-audit-log/
├── spec.md
├── plan.md              # this file
├── research.md          # D1-D9
├── data-model.md        # audit_entries, the migration, what did not change
├── quickstart.md        # validation scenarios
├── contracts/
│   ├── http-api.md      # GET /api/audit, /api/audit/{id}, /api/audit/summary
│   └── messages.md      # AuditEntryRecorded
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

```text
server/src/BuildingBlocks/
├── Ecommerce.Contracts/Activity/AuditEntryRecorded.cs
└── Ecommerce.Shared/Audit/AuditTrail.cs          # AuditCategory, AuditActor, IAuditTrail, AuditTrail,
                                                  # AuditSnapshot (redaction), AddAuditTrail

server/src/Services/Activity/
├── Ecommerce.Activity.Domain/Entities/AuditEntry.cs
├── Ecommerce.Activity.Application/
│   ├── Audit/AuditDiff.cs
│   ├── Audit/AuditResponses.cs
│   ├── Audit/Commands/RecordAuditEntryCommand.cs
│   ├── Audit/Queries/AuditQueries.cs             # list, one, summary
│   ├── Common/Interfaces/IAuditRepository.cs
│   └── Common/PagedResponse.cs
├── Ecommerce.Activity.Infrastructure/
│   ├── Migrations/20260923192252_InitialCreate.cs
│   └── Persistence/{ActivityDbContext.cs, Configurations/AuditEntryConfiguration.cs,
│                    Repositories/AuditRepository.cs}
└── Ecommerce.Activity.WebApi/
    ├── Consumers/RecordAuditEntryConsumer.cs
    ├── Controllers/{ApiControllerBase.cs, AuditController.cs}
    └── Program.cs                                # port 5063, "ActivitySvc" endpoint prefix

server/tests/Ecommerce.Activity.Tests/            # AuditDiffTests, AuditLogTests, AuditTrailTests,
                                                  # RedactionTests, ActivityTestFixture
server/tests/Ecommerce.{Identity,Catalog,Inventory,Order,Payment}.Tests/AuditTests.cs

client/src/services/audit/{index.ts, types.ts}
client/src/hooks/audit/index.ts
client/src/pages/admin-audit/{index.tsx, entry-dialog.tsx, format.ts}
```

Instrumented handlers (each calls `IAuditTrail.RecordAsync` before its save, or inside a `stage`):

- Identity: `LoginCommandHandler`, `RegisterCommandHandler`, `RegisterSellerCommand`, `SellerCommands`
  (rename), `SaveAddress` / `UpdateAddress` / `DeleteAddress` handlers; `Common/AuditActors.cs`.
- Catalog: category create/delete, product create/delete, `SetVariantPriceCommand`,
  `SetProductTranslationCommand`, `AddProductVariantCommand`, `UpdateProductVariantCommand`, product and
  variant image commands, `OrphanImages`; `Common/CatalogAudit.cs`.
- Inventory: `SetStockOnHandCommandHandler`, `ExpireStockCommandHandler`,
  `RestockCancelledOrderCommandHandler`.
- Order: `SubmitOrderCommandHandler`, `CancelOrderCommands`, `DeliveryCommands`, `FulfilmentStep`,
  `SellerFulfilmentCommands`, `RecordPayoutCommand`; `Orders/Common/ParcelAudit.cs`; `stage` parameters on
  `IOrderRepository` / `IPayoutRepository` and their implementations.
- Payment: `ChargeOrderCommandHandler`, `RefundOrderCommandHandler`.

Touched outside the services: `server/Ecommerce.slnx`, `server/Dockerfile`, `server/docker-compose.yml`
(`postgres-activity` on 5440), `server/docker-compose.app.yml`, `server/.env.example`
(`ACTIVITY_DB_PORT=5440`), `server/start-dev.ps1` and `.sh`, the gateway's `appsettings.json` (routes
`audit-route`, `audit-root-route`, `activity-health-route`, cluster `activity-cluster`),
`.github/workflows/ci.yml` (test database, image and publish lists), `bruno/admin-audit/` and two
`bruno/security-checks/` requests, `client/src/routes/index.tsx` and the admin layout.

**Structure Decision**: Activity mirrors the other services' four-project split. The audit abstraction
goes in `Ecommerce.Shared` because five services call it identically; putting a copy in each would be the
per-service duplicate CLAUDE.md warns against.

## Complexity Tracking

| Addition | Why | Simpler alternative rejected |
| :-- | :-- | :-- |
| A ninth service | Audit (and #87 notifications) are fed by every service and owned by none | Tables in each service: no single place to read, and cross-service queries |
| Audit + notifications in one service | Both are event sinks; halves the infra | Two services (user's choice was one) |

## What this feature does not finish

- **Moderation** is a category with no actions yet; roles, locks, approvals and takedowns arrive with
  #88-#91 (specs/043-045).
- Nobody but an administrator reads anything: moderators reading their own decisions came in specs/045
  (`GET /api/audit/mine`).
- No retention, archiving or export.
- A product image's entry is saved just after the image switch, not with it (the switch is the specs/019
  guarded `UPDATE`), so a crash between the two can leave a switch with no entry.
