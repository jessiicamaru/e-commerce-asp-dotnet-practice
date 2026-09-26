# Implementation Plan: A deleted product's holds are released with its stock

**Branch**: `090-deleted-product-reservations` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #181

## Summary

`ForgetProductCommand` (Inventory's answer to `ProductDeletedEvent`) now runs in one transaction: it deletes the
variants' stock rows as before, then releases their `Held` reservations with one guarded `UPDATE`
(`Released`, "Product deleted"). Settled reservations stay as history. No schema, contract or status change. The
misleading "reservations cascade" comment is corrected.

## Technical Context

- `server/src/Services/Inventory/Ecommerce.Inventory.Application/Stock/Commands/ForgetProduct/ForgetProductCommand.cs`
- `server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/Interfaces/IReservationRepository.cs`
- `server/src/Services/Inventory/Ecommerce.Inventory.Infrastructure/Persistence/Repositories/ReservationRepository.cs`,
  `StockRepository.cs` (comment)
- `server/tests/Ecommerce.Inventory.Tests/ForgetProductTests.cs`

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core 10 (`ExecuteUpdateAsync`), Npgsql, MediatR 12.4.1, MassTransit 8.3.6 (consumer only)

**Storage**: PostgreSQL `ecommerce_inventory_db` (5437) - `stock_items`, `stock_reservations`, unchanged schema

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Inventory.Tests`); mutation checks

**Target Platform**: Inventory (5060), consumer of `ProductDeletedEvent`

**Performance Goals**: one extra indexed-by-status `UPDATE` per product deletion - negligible

**Constraints**: nothing an earlier image cannot parse; idempotent under redelivery; no stock movement

**Scale/Scope**: one repository method, one handler, five tests

## Design

- `IReservationRepository.ReleaseHeldAsync(productIds, reason, now)` - `ExecuteUpdateAsync` over
  `ProductId IN (...) AND Status = Held`, returning the count.
- `ForgetProductCommandHandler` takes `IUnitOfWork` and `IReservationRepository`; inside
  `ExecuteInTransactionAsync`: `ForgetAsync` then `ReleaseHeldAsync` (order: research D3). `Reason` is a public
  constant, `"Product deleted"`. The log line reports both counts. The return value (stock rows forgotten) is unchanged.
- `StockRepository.ForgetAsync`'s comment now says nothing cascades and who releases the holds.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Inventory acts on its own tables in answer to an event it already consumes; no new message, no call to another service. |
| **II. Clean Architecture Layering** | **Pass.** The decision is in the Application handler; the statement is in the Infrastructure repository behind an Application interface; the consumer is unchanged. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The deletion and the release commit in one transaction or not at all; the release is guarded on `Held`, so a redelivered event changes nothing; nothing is published, so there is no outbox ordering to get wrong. |
| **IV. Identity Comes From the Token** | **Not applicable** - a consumer of a system event; no caller, no token. |
| **V. Evidence Over Assumption** | **Pass.** What each settlement path does without a stock row was established by reading every handler (spec table) and pinned by tests; three tests failed before the fix; two mutations were each caught; the suite is 57/57 against real PostgreSQL. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/090-deleted-product-reservations/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # stock_reservations; the new transition
├── quickstart.md
├── contracts/
│   └── messages.md      # ProductDeletedEvent, unchanged; what its consumer now does
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- The four source files and one test file above.
- `CLAUDE.md`, `docs/features/catalog.md`, `docs/features/shopping-and-checkout.md`, `docs/project/backlog.md`,
  `docs/project/timeline.md`

No endpoint, message, table or gateway route changes, so `docs/reference/` is not regenerated.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- An order for a product deleted before it settled still completes or fails as the saga decides; Inventory no longer
  holds units for it, but Order is not told. Out of scope (spec).
- Reservations are still not tied to stock rows by a key (research D2).
