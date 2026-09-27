# Implementation Plan: A seller cancels the part of an order they cannot fulfil

**Branch**: `104-seller-cancels-part` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #211

## Summary

A seller, or an administrator for the shop's own goods, cancels one part of a paid order before it ships, with a
reason. Order marks the part with cancellation columns under the order's row lock. When other parts remain, it
publishes `OrderPartCancelledEvent`: Inventory returns the part's reservations, and Payment records a refund of the
part's goods and tax. When no part remains, the order is cancelled whole through specs/039's path. Payment's
whole-order refund becomes the remainder after part refunds. Summaries, earnings and insights stop counting a
cancelled part.

## Technical Context

- **Contracts**: `Order/OrderSubmittedEvent.cs` (`OrderPartCancelledEvent`).
- **Order**:
  - Domain `OrderShipment` gains four columns.
  - Infrastructure:
    - migration `AddPartCancellation`;
    - `OrderRepository`: `TryCancelPartAsync`, with cancelled parts excluded from the summary, the move guard and
      the whole-order cancel;
    - `PayoutRepository.Earning` and `OrderInsights`;
    - `VoucherRepository.ReleaseForSellerAsync`.
  - Application: `Orders/Commands/CancelOrder/CancelPart.cs` (new), `OrderNotices`, and the part responses.
  - WebApi: two routes.
- **Inventory**: `RestockCancelledOrderCommand` gains an optional variant filter; `RestockCancelledPartConsumer`.
- **Payment**: `Refund.PartId` and its migration; a part refund command; `RefundCancelledPartConsumer`;
  `RefundOrderCommandHandler` refunds the remainder.
- **Shared**: `NotificationKind.PartCancelled` and `notification-kinds.json`.
- **Storefront**: the seller's sale page (cancel with a reason), the admin order page (the shop's part), the buyer's
  order page (a cancelled part), and words in en and vi.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, MassTransit (EF outbox); TanStack Query

**Storage**: 4 columns in `ecommerce_order_db`; 1 column and 2 indexes in `ecommerce_payment_db`

**Testing**: xUnit against real PostgreSQL (Order, Inventory, Payment); Vitest; Bruno; `verify-saga.sh`

**Target Platform**: Order (5059), Inventory (5060), Payment (5061), the storefront

**Performance Goals**: one extra locked transaction per cancel; nothing on the checkout path

**Constraints**: never refund a part twice; stock back exactly once; the order must not wait on a cancelled part

**Scale/Scope**: 2 routes, 1 message, 3 services, 3 pages

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order decides the part; Inventory and Payment act on their own rows from one event; no synchronous call is added. |
| **II. Clean Architecture Layering** | **Pass.** Rules in Application (`CancelPart`), the locked transaction in the repository, routes only send. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (by design).** The part's columns, the event (or the whole-order event), the voucher release, the audit entry and the notice commit in one transaction under the order's lock. The consumers are idempotent: reservation status guards in Inventory, and a unique `refunds.PartId` in Payment. |
| **IV. Identity Comes From the Token** | **Pass.** The seller is the token's subject; no seller id in the request; another's part is a 404. |
| **V. Evidence Over Assumption** | **Pass.** `PartCancellationTests` (6), `RestockCancelledPartTests` (3), `PartRefundTests` (2) against real databases, 7 client tests; eleven mutations each caught (quickstart Scenario 4, plus the seller voucher, the whole-refund lookup, and three client ones); full suites Order 299, Inventory 78, Payment 28, Activity 40, client 526; Bruno 318/318 and `verify-saga.sh` through rebuilt containers; a **live check** through the broker - part refund 20,350,000 plus remainder 6,072,000 equals the 26,422,000 charged, every unit back. |

**Post-design re-check** (after implementation): no violations. Two defects the design had to rule out were found in Payment while building it and are covered by tests: the whole-refund lookup and the payments page both counted a part refund as the whole one.

## Project Structure

### Documentation (this feature)

```text
specs/104-seller-cancels-part/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D8
├── data-model.md        # Columns, indexes, the transaction, readers changed
├── quickstart.md
├── contracts/
│   ├── http-api.md      # the two routes and the read shapes
│   └── messages.md      # OrderPartCancelledEvent, PartCancelled
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/fulfilment-and-delivery.md` (the "whole-order only" limit goes),
`docs/features/marketplace.md`, `docs/features/audit-and-notifications.md`, CLAUDE.md, backlog, timeline, and a run of
`generate_reference.py`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.
