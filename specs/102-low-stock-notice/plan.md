# Implementation Plan: A seller is told when a variant runs low

**Branch**: `102-low-stock-notice` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #200

## Summary

Inventory gains a per-variant `LowStockThreshold`, with a configured shop default of 5. The reservation handler judges
each variant's crossing from before and after values inside its locked transaction, and stages `StockRanLowEvent` when
available stock falls from at or above the line to below it. Catalog consumes the event and notifies the product's
seller (`StockRunningLow`, with the product and the count left), and nobody for the shop's own products. Sellers set the
threshold beside the stock count.

## Technical Context

- **Contracts**: `Inventory/StockRanLowEvent.cs` (new).
- **Inventory Domain**: `StockItem.LowStockThreshold`.
- **Inventory Application**:
  - `Common/LowStock.cs` (new): the pure `Crossed` rule and the options.
  - `ReserveStockCommandHandler`: before and after values, and the publish.
  - `Stock/Commands/SetLowStockThreshold` (new): command, validator and handler with `StockOwnership`.
  - `StockResponse` gains two fields, and the mapping follows.
- **Inventory Infrastructure**: the configuration (CHECK), migration `AddLowStockThreshold`, and options binding with
  startup validation.
- **Inventory WebApi**: `StockController` gains `PUT {variantId}/low-stock-threshold`, and `appsettings.json` gains
  `Inventory:LowStock:DefaultThreshold`.
- **Catalog**: `StockRanLowConsumer` (WebApi) and a `NotifyLowStockCommand` handler (Application), which reads the
  variant and product through existing repositories; the consumer is registered.
- **Shared**: `NotificationKind.StockRunningLow`, and `notification-kinds.json` (the kind plus the `left`
  placeholder).
- **Storefront**:
  - `services/stock` (`setLowStockThreshold`, and two fields on the model) and `hooks/stock`.
  - A threshold field in `components/seller/variant-editor`.
  - Words in seller and notifications, en and vi.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, MassTransit outbox and inbox, FluentValidation; TanStack Query

**Storage**: one column in `ecommerce_inventory_db` (5437)

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Inventory (5060), Catalog (5057) and the storefront

**Performance Goals**: no added query in the reservation path. The threshold is on the row already locked.

**Constraints**: the notice commits with the stock change; checkout gains no synchronous call; the queue name is unique

**Scale/Scope**: 1 column, 1 event, 1 route, 1 consumer, 1 notice kind, 1 field in the editor

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before the code was written.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Inventory decides from its own rows and announces. Catalog words the notice from its own data. No synchronous edge is added, which is why the event was chosen over the issue's gRPC option (research D1). |
| **II. Clean Architecture Layering** | **Pass.** The crossing rule is pure Application code (`LowStock.Crossed`), storage is in Infrastructure, and the controller and consumer only send. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The event is staged in the reservation's transaction before its one save. The publisher is idempotent through the reservation's duplicate check, and the consumer through the inbox. |
| **IV. Identity Comes From the Token** | **Pass.** The threshold endpoint takes no seller id; ownership comes from the token through `StockOwnership`. |
| **V. Evidence Over Assumption** | **Pass.** 18 Inventory tests and 2 Catalog tests against real databases, 3 client tests; seven mutations each caught and one found equivalent and recorded (quickstart Scenario 4); full suites Inventory 75, Catalog 241, Activity 40, Order 286, client 516; Bruno 314/314 and `verify-saga.sh` through rebuilt containers; the `CatalogSvcStockRanLow` queue bound with one consumer. |

**Post-design re-check** (after implementation): no violations. The event is staged before the reservation's one save
(III); Inventory makes no call to Catalog (I); the threshold route reads the seller from the token and asks ownership
live, as setting stock does (IV).

## Project Structure

### Documentation (this feature)

```text
specs/102-low-stock-notice/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D5
├── data-model.md        # The column, the crossing, the notice kind
├── quickstart.md
├── contracts/
│   ├── http-api.md      # PUT /api/stock/{id}/low-stock-threshold, stock response fields
│   └── messages.md      # StockRanLowEvent, StockRunningLow
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/marketplace.md` (stock), `docs/features/audit-and-notifications.md`,
CLAUDE.md, the backlog and the timeline, and a run of `generate_reference.py`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.
