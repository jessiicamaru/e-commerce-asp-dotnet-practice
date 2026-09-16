# Implementation Plan: Payment Service

**Branch**: `002-payment-service` | **Date**: 2026-09-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-payment-service/spec.md`

## Summary

Add an `Ecommerce.Payment` service that consumes `ProcessPaymentCommand`, records the outcome in its
own database, and replies so `OrderStateMachine` can reach `OrderCompleted`. It is a **stand-in**: it
approves without contacting any provider and without moving money.

That makes checkout complete for the first time. Until now an order reached `InventoryReservedState`
and stopped, and its held stock was eventually reclaimed by Inventory's expiry sweeper as though the
order had failed.

Two things shape the design beyond the obvious:

- **A stub that is mistaken for the real thing fulfils every order for free.** Three independent
  signals make that hard: `Provider = "Stub"` on every row, a startup warning, and `/health`
  reporting both the stub nature and the configured outcome (research D3).
- **The saga's compensation branch has never run.** `PaymentFailed` → `ReleaseInventoryCommand` →
  stock returned is untested code guarding a real failure. A service-wide `PAYMENT_OUTCOME` setting
  makes it reachable deliberately (research D2), which is what the user chose when asked.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (RabbitMQ transport, EF Core outbox and inbox),
MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3, `Ecommerce.Shared`,
`Ecommerce.Contracts`

**Storage**: PostgreSQL 16, database `ecommerce_payment_db`, host port 5438

**Testing**: xUnit with MassTransit's test harness, against a real PostgreSQL — a second test
project mirroring `Ecommerce.Inventory.Tests` (research D6)

**Target Platform**: Linux container / Windows dev host, HTTP on port 5061

**Project Type**: Backend microservice, Clean Architecture — Domain, Application, Infrastructure,
WebApi

**Performance Goals**: An order completes within 5 seconds of payment being requested at the 99th
percentile (SC-002). No throughput target

**Constraints**: One payment per order under any interleaving (FR-005, FR-006, SC-005). Every
request gets exactly one reply (FR-001). The record and its reply share one transaction. No
credential, card number or personal payment detail is ever handled

**Scale/Scope**: Single instance. One entity, one consumer, two query endpoints. Substantially
smaller than feature 001

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Own database on 5438; no new shared contracts. Research D4 records the consequence honestly: the service cannot verify the amount, because doing so would mean reading Order's data or calling it at runtime. The limitation is documented rather than solved by breaching the boundary |
| **II. Clean Architecture Layering** | **Pass.** Four projects mirroring Inventory. Application depends on `MassTransit.Abstractions` only |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The payment row and its reply commit together — stage, publish, then one `SaveChangesAsync`. Idempotency rests on a unique `OrderId` constraint, database-enforced as the principle demands. The row is immutable after insert, so replay needs no guarded update |
| **IV. Identity Comes From the Token** | **Pass.** Query endpoints are `Admin` only; no endpoint accepts a caller-supplied identity. Note the `UserId` on `ProcessPaymentCommand` is service-to-service data on a message, not a caller-supplied field on an HTTP request — the principle concerns the latter |
| **V. Evidence Over Assumption** | **Pass.** SC-005 runs against a real PostgreSQL because the unique constraint is the guarantee. Research D6 carries forward a specific lesson from feature 001 rather than relearning it: the inventory concurrency test first failed on `max_connections` while appearing to pass earlier, so this fixture bounds its pool from the start |

**Post-Phase 1 re-check**: no violations. Complexity Tracking is empty — where a simpler option
existed it was taken.

One thing worth stating plainly rather than burying: **this feature deliberately ships something
that does not work for real.** That is not a constitution violation — it is a scaffold, chosen
knowingly — but it is the kind of decision that should be visible in the plan rather than discovered
later in the code.

## Project Structure

### Documentation (this feature)

```text
specs/002-payment-service/
├── plan.md              # This file
├── spec.md              # 4 user stories, 12 requirements, 8 success criteria
├── research.md          # Phase 0: six decisions with rejected alternatives
├── data-model.md        # Phase 1: one table, outcomes, transaction rules
├── quickstart.md        # Phase 1: seven validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
├── contracts/
│   ├── messages.md      # Consumed and published messages
│   └── http-api.md      # Payment lookup and health
└── tasks.md             # Phase 2 — created by /speckit-tasks
```

### Source Code (repository root)

```text
server/src/Services/Payment/
├── Ecommerce.Payment.Domain/
│   ├── Entities/            # Payment
│   └── Enums/               # PaymentStatus
├── Ecommerce.Payment.Application/
│   ├── Common/Interfaces/   # IPaymentRepository, IUnitOfWork
│   ├── Payments/Commands/   # ProcessPayment
│   ├── Payments/Queries/    # GetPaymentByOrderId, GetPayments
│   └── DependencyInjection.cs
├── Ecommerce.Payment.Infrastructure/
│   ├── Persistence/         # PaymentDbContext, Configurations, Repositories
│   ├── Gateway/             # PaymentOutcomeOptions — the stub's only decision
│   └── DependencyInjection.cs
└── Ecommerce.Payment.WebApi/
    ├── Consumers/           # ProcessPaymentConsumer
    ├── Controllers/         # PaymentsController
    └── Program.cs

server/tests/
└── Ecommerce.Payment.Tests/
```

Touched outside the new service:

- `server/Ecommerce.slnx` — five new projects
- `server/docker-compose.yml` — `postgres-payment` on 5438
- `server/.env.example` — `PAYMENT_DB_PORT`, `PAYMENT_OUTCOME`
- `server/start-dev.ps1`, `start-dev.sh` — migration and launch
- `src/ApiGateway/.../appsettings.json` — route, cluster, health route
- `.github/workflows/ci.yml` — payment database service

**Structure Decision**: Mirrors `Services/Inventory` exactly, which itself mirrors Catalog and
Order. The one addition is `Infrastructure/Gateway/`, holding the single decision the stub makes —
named so that the seam a real provider would replace is obvious.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature finishes, and what it does not

**Finishes**: the saga runs end to end. Submit → reserve → pay → complete → stock deducted, with no
message published by hand. Both the success and compensation branches become reachable.

**Does not**: take money. There is no provider, no card handling, no refund, no reconciliation. The
next real step is replacing `Infrastructure/Gateway/` with an integration, at which point every
guarantee here — one payment per order, atomic record-and-reply, replay safety — starts protecting
actual money rather than a row in a table.
