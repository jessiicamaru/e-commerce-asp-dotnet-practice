# Implementation Plan: One Source of Truth for Stock

**Branch**: `004-stock-single-source` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-stock-single-source/spec.md`, tracked as issue
[#4](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/4)

## Summary

Catalog stops inventing a stock number and starts reporting one it was told. Inventory announces a
product's availability whenever it changes; Catalog keeps that answer as a read model and exposes it
as `availability` on the product payload. `StockQuantity` leaves both the product payload and the
create-product request.

Three things shape the design beyond the obvious:

- **Six handlers change stock, not one.** `ReserveStock`, `ReleaseStock`, `ConfirmStock`,
  `ExpireStock`, `SetStockOnHand` and `RegisterProduct` all move availability. Every one must
  announce, and "six places to remember" is the real risk in this feature — bigger than anything in
  the consumer (research D2).
- **Ordering, not just duplication.** A redelivered announcement is the easy half. Two announcements
  overtaking each other is the half that silently leaves a product readable as available when it is
  not, and it needs a *comparison*, not just a guard (research D3).
- **This is a breaking API change, and the repo has no frontend to break.** That makes it cheap
  now and expensive later, which is the argument for doing it in this shape rather than adding
  `availability` alongside the lie (research D5).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (RabbitMQ transport, EF Core outbox and inbox),
MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3, `Ecommerce.Shared`,
`Ecommerce.Contracts`

**Storage**: PostgreSQL 16 — `ecommerce_catalog_db` (5433) gains a column and loses one;
`ecommerce_inventory_db` (5437) is unchanged

**Testing**: xUnit against a real PostgreSQL. A new `Ecommerce.Catalog.Tests` for the consumer's
ordering and idempotency guarantees, and additions to the existing `Ecommerce.Inventory.Tests` for
the six announcement paths (research D6)

**Target Platform**: Linux container / Windows dev host. Catalog on 5057, Inventory on 5060

**Project Type**: Two existing backend microservices, Clean Architecture. No new service

**Performance Goals**: The listing reflects a change within 10 seconds at the 99th percentile
(FR-010). The listing itself must not get slower — availability is a column on the product row, not
a call

**Constraints**: One new record in `Ecommerce.Contracts`, which every service deserializes.
`ProductResponse` and `CreateProductCommand` change shape — breaking for any API consumer. The
consumer must be both idempotent **and** order-tolerant (FR-006, FR-007)

**Scale/Scope**: One new contract record, one consumer, six publish sites, two migrations, two test
projects touched

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass — this feature exists to restore it.** Today Catalog stores a stock figure it owns nothing about and shows it to shoppers, which is the principle's named failure: *"the non-owning copy MUST NOT inform any decision — most importantly, a sell/no-sell decision."* After this, Inventory is the sole owner and Catalog holds a **record of what it was told**, marked as such in the data model. The copy still exists — that is what a read model is — but it is fed by its owner and is explicitly not authoritative. The distinction that matters: nothing sells, reserves or charges against Catalog's copy; it is display only, and the reservation path still reads Inventory under a row lock |
| **II. Clean Architecture Layering** | **Pass.** The announcement is published from Application handlers through `IPublishEndpoint` (already a dependency there); Catalog's consumer lives in WebApi and dispatches through MediatR, like every other consumer in the repo |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each of the six publish sites stages its entity change, publishes, then calls `SaveChangesAsync` **once** — the existing shape in `ReserveStockCommandHandler`, not a new one. On the consuming side idempotency is a guarded update comparing the announcement's observation time against the one already recorded, so both a redelivery **and** an out-of-order delivery affect zero rows. Enforced in the `WHERE` clause, not in configuration |
| **IV. Identity Comes From the Token** | **Pass, and untouched.** No endpoint gains or loses an identity concern. `GET /api/products` stays `[AllowAnonymous]`; `POST /api/products` stays `Admin`-only. Removing `stockQuantity` from the create request removes a field, not a check |
| **V. Evidence Over Assumption** | **Pass.** The claim that six handlers move stock was read out of the code, not assumed from the domain. The claim that Inventory announces no level today was likewise verified. The ordering guarantee is tested by delivering announcements in reverse, and both guarantees are mutation-checked: removing the timestamp comparison must fail the out-of-order test, and removing the guard must fail the redelivery test |

**Post-Phase 1 re-check**: no violations. Complexity Tracking is empty.

One thing worth stating plainly: **a read model is still a copy, and this plan keeps one.** The
principle forbids a non-owning copy *informing a decision*, not its existence — it explicitly
contemplates "a value duplicated elsewhere for display". The line this design must not cross is
letting anything sell against Catalog's number. It does not: checkout reserves against Inventory's
row under `FOR UPDATE`, exactly as before. If a later change ever reads Catalog's availability to
decide whether an order may proceed, that is the violation, and it will not look like one.

## Project Structure

### Documentation (this feature)

```text
specs/004-stock-single-source/
├── plan.md              # This file
├── spec.md              # 3 user stories, 12 requirements, 7 success criteria
├── research.md          # Phase 0: six decisions with rejected alternatives
├── data-model.md        # Phase 1: the column that goes, the column that arrives
├── quickstart.md        # Phase 1: validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
├── contracts/
│   ├── messages.md      # The one new record, and who publishes it
│   └── http-api.md      # What changes shape on the product endpoints
└── tasks.md             # Phase 2 — created by /speckit-tasks
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Contracts/
└── Inventory/                          # + StockAvailabilityChangedEvent

server/src/Services/Inventory/
├── Ecommerce.Inventory.Application/
│   ├── Common/                         # + the announcement helper, one implementation
│   ├── Reservations/{ReserveStock,ReleaseStock,ConfirmStock,ExpireStock}/
│   └── Stock/Commands/{RegisterProduct,SetStockOnHand}/
│                                       # all six publish before their single SaveChangesAsync
└── (no schema change)

server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/  # Product: StockQuantity out, Availability in
├── Ecommerce.Catalog.Application/
│   ├── Products/Commands/CreateProduct/# stockQuantity removed from command + validator
│   └── Products/Availability/          # NEW — the guarded, time-compared update
├── Ecommerce.Catalog.Infrastructure/
│   └── Migrations/                     # one migration: drop a column, add two
└── Ecommerce.Catalog.WebApi/
    ├── Consumers/                      # NEW — StockAvailabilityChangedConsumer
    └── Program.cs                      # + AddConsumer, + inbox callback, + endpoint prefix

server/tests/
├── Ecommerce.Catalog.Tests/            # NEW
└── Ecommerce.Inventory.Tests/          # + six announcement tests
```

Touched outside those:

- `server/Ecommerce.slnx`, `.github/workflows/ci.yml` — one new test project and its database
- `CLAUDE.md`, `docs/architecture/microservices-design.md` — both describe the current ownership

**Structure Decision**: Catalog gains its first consumer, so it needs the same three lines Order
needed in feature 003 — `AddConsumer`, the inbox callback, and **an endpoint name prefix**. The
prefix is not optional: feature 003 shipped a defect where two services' consumer classes collided
on one queue and competed for it. `StockAvailabilityChangedConsumer` is a name no other service is
likely to take, but relying on that is how the last collision happened.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature finishes, and what it does not

**Finishes**: a shopper's view of whether they can buy something comes from the service that would
have to supply it. The catalogue stops reporting a number no code can change.

**Does not**: make the listing transactionally consistent with inventory. It is a read model fed by
messages, so there is a window — bounded by FR-010 at 10 seconds — in which a product can read as
available while its last unit is being reserved by somebody else. That is inherent to the
architecture, not a shortcoming of this feature, and it is why checkout still reserves under a row
lock rather than trusting the listing. Nor does it add a low-stock threshold, back-in-stock alerts,
or any backfill for products created before the announcements started.
