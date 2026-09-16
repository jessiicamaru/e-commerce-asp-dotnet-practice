# Implementation Plan: Inventory Reservations

**Branch**: `001-inventory-reservations` | **Date**: 2026-09-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-inventory-reservations/spec.md`

## Summary

Add an `Ecommerce.Inventory` microservice that answers the stock question the checkout saga has
been asking into the void. It owns stock levels and reservations in its own PostgreSQL database,
consumes `ReserveInventoryCommand` and `ReleaseInventoryCommand`, and replies with
`InventoryReservedEvent` or `InventoryReservationFailedEvent` so `OrderStateMachine` can leave the
`Submitted` state.

The approach is deliberately the simple one: an aggregated counter per product guarded by a
pessimistic row lock, consumers made idempotent by both the MassTransit inbox and a unique
`(OrderId, ProductId)` constraint, and a background sweeper that returns stranded stock. Reasoning
and rejected alternatives are in [research.md](./research.md).

Two things the spec did not anticipate surfaced during design and are folded in:

- **Nothing confirms a successful order.** The saga publishes `OrderCompletedEvent` and finalizes;
  no message tells inventory to commit the reservation. Without handling this, every successful
  order's units stay held until the sweeper wrongly returns them to the shelf — re-selling goods
  already shipped. Inventory consumes `OrderCompletedEvent` (research D2).
- **`Product.StockQuantity` in Catalog becomes descriptive only.** It is not removed, but nothing
  may read it for an availability decision (research D5).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (RabbitMQ transport, EF Core outbox and inbox),
MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3, `Ecommerce.Shared`,
`Ecommerce.Contracts`

**Storage**: PostgreSQL 16, database `ecommerce_inventory_db`, host port 5437

**Testing**: `MassTransit.Testing` harness for consumer behaviour; xUnit; concurrency and
idempotency cases against a real PostgreSQL (research D7). This is the repository's first test
project

**Target Platform**: Linux container / Windows dev host, HTTP on port 5060

**Project Type**: Backend microservice, Clean Architecture — Domain, Application, Infrastructure,
WebApi

**Performance Goals**: Reservation decided within 5 seconds at the 99th percentile (SC-002,
SC-003). No throughput target at this stage

**Constraints**: No overselling under any interleaving (FR-007, SC-004). Every reserve request gets
exactly one reply (FR-002). Replay must not move stock twice (FR-006). Stock changes and their
outgoing messages share one transaction

**Scale/Scope**: Single instance per service. Catalogue in the hundreds of products, orders in the
tens per day. Explicitly not sized for flash sales — see research D1 for the trigger that would
change the storage design

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

⚠️ **`.specify/memory/constitution.md` is the unedited template** — every principle is still a
`[PRINCIPLE_N_NAME]` placeholder. There are no ratified principles to check against, so this gate
passes vacuously rather than genuinely. Run `/speckit-constitution` to establish real principles;
until then this section carries no weight and should not be read as approval.

In their absence, the design was checked against the conventions the codebase actually demonstrates:

| Convention observed in the codebase | How this feature complies |
| :--- | :--- |
| Clean Architecture, Domain with no dependencies | Four projects matching Catalog and Order exactly |
| Database per service | Own database on 5437; no cross-service database access |
| CQRS through MediatR, feature-foldered by use case | Commands and queries under `Application/Stock/...` |
| Transactional outbox: stage, publish, then `SaveChangesAsync` once | Every consumer follows it; see data-model transaction rules |
| Cross-service coupling only through `Ecommerce.Contracts` | No new shared contracts added at all |
| Fluent API mapping, snake_case plural tables, `Guid.CreateVersion7()` keys | `stock_items`, `stock_reservations` |
| Errors as RFC 7807 through `Ecommerce.Shared` | Reuses `GlobalExceptionHandler`, `AddJwtAuthentication` |

**Post-Phase 1 re-check**: no violations introduced. The Complexity Tracking table below is empty
because nothing in the design needed justifying against a simpler alternative — where a simpler
option existed, it was taken (research D1, D2, D4).

## Project Structure

### Documentation (this feature)

```text
specs/001-inventory-reservations/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: seven decisions with rejected alternatives
├── data-model.md        # Phase 1: tables, state machine, locking rules
├── quickstart.md        # Phase 1: eight validation scenarios mapped to success criteria
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
├── contracts/
│   ├── messages.md      # Consumed and published messages
│   └── http-api.md      # Stock and reservation endpoints
└── tasks.md             # Phase 2 — created by /speckit-tasks, not by this command
```

### Source Code (repository root)

```text
server/src/Services/Inventory/
├── Ecommerce.Inventory.Domain/
│   ├── Entities/            # StockItem, StockReservation
│   └── Enums/               # ReservationStatus
├── Ecommerce.Inventory.Application/
│   ├── Common/Interfaces/   # IStockRepository, IReservationRepository
│   ├── Stock/Commands/      # SetStockOnHand
│   ├── Stock/Queries/       # GetStockByProductId, GetStock
│   ├── Reservations/        # ReserveStock, ReleaseStock, ConfirmStock, ExpireStock
│   ├── Reservations/Queries/# GetReservationsByOrderId
│   └── DependencyInjection.cs
├── Ecommerce.Inventory.Infrastructure/
│   ├── Persistence/
│   │   ├── InventoryDbContext.cs
│   │   ├── Configurations/
│   │   └── Repositories/    # Row-locking reads live here
│   ├── BackgroundServices/  # ReservationExpirySweeper
│   └── DependencyInjection.cs
└── Ecommerce.Inventory.WebApi/
    ├── Consumers/           # ReserveInventory, ReleaseInventory, OrderCompleted, ProductCreated
    ├── Controllers/         # StockController, ReservationsController
    └── Program.cs

server/tests/
└── Ecommerce.Inventory.Tests/   # First test project in the repository
```

Touched outside the new service:

- `server/Ecommerce.slnx` — five new projects
- `server/docker-compose.yml` — `postgres-inventory` on 5437
- `server/.env.example` — `INVENTORY_DB_PORT`, `INVENTORY_RESERVATION_TTL_MINUTES`
- `server/start-dev.ps1`, `start-dev.sh` — migration and launch
- `src/ApiGateway/.../appsettings.json` — route, cluster, health route
- `.github/workflows/ci.yml` — inventory database service, the new test project

**Structure Decision**: Mirrors `Services/Catalog` and `Services/Order` exactly — same four-project
split, same folder names, same DI entry points. Consumers sit in WebApi alongside Controllers
because that is where MassTransit is configured, matching where the existing services put their bus
wiring. The deviation from existing practice is `server/tests/`, which has no precedent because the
repository has no tests yet; it follows the conventional .NET layout.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

Stated plainly so the next reader is not misled: this makes the saga progress **one step further**,
not all the way. With no payment service, an order reaches `InventoryReservedState` and stops.
Every such reservation will be expired by the sweeper after its holding period, which is correct
behaviour and not a bug. Exercising the confirm path before a payment service exists means
publishing `PaymentProcessedEvent` by hand — quickstart scenario 5.
