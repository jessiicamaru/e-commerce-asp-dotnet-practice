# Implementation Plan: Cancelling a paid order

> Completed on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Branch**: `039-order-cancellation` | **Spec**: [spec.md](spec.md) | [research](research.md) |
[contracts](contracts/api.md)

## Summary

Let a paid order be cancelled - by its customer while every parcel waits, by an administrator until the
first parcel ships, whole orders only - and undo what it took. Order takes the same row lock as every
parcel move, reads the parts, runs a guarded `UPDATE` to `Cancelled` (recording `CancelledBy`) and stages
`OrderCancelledEvent` through its outbox, in one transaction. Inventory puts back what its own
reservations say; Payment records one refund of what its own payment row says, in a new `refunds` table.
Balances and payouts stop counting cancelled orders. No status an older image cannot parse is added.
Decisions in [research.md](research.md).

## Technical Context

- **Contracts**: `OrderCancelledEvent`.
- **Order**: `orders.CancelledBy` (migration `AddOrderCancelledBy`); `IOrderRepository.TryCancelAsync`
  (lock, ensure parts, decide, guarded UPDATE, stage the event, save - one transaction);
  `CancelMyOrderCommand`, `CancelOrderCommand` (staff); `Sales.Earning` split from `Sales.Statuses`;
  `ShipmentMoveOutcome.OrderCancelled`; routes per the contract.
- **Inventory**: `RestockCancelledOrderCommand` + consumer (research D4), registered in `Program.cs`.
- **Payment**: `Refund` entity, `refunds` table (migration `AddRefunds`); `RefundOrderCommand` + consumer;
  payment responses carry the refund.
- **Client**: cancel on the customer's order page (AlertDialog), on the staff order page; "Cancelled"
  wherever an order or sale status is shown; a cancelled sale shows no steps.
- **verify-saga.sh**: a cancellation scenario - place, pay, cancel, then stock back to the start and a
  refund recorded.

**Language/Version**: C# / .NET 10; TypeScript / React 19; bash for `verify-saga.sh`

**Primary Dependencies**: MassTransit 8.3.6 with the EF Core outbox (Order) and consumers (Inventory,
Payment), EF Core with Npgsql, MediatR

**Storage**: `ecommerce_order_db` - `20260923165912_AddOrderCancelledBy`; `ecommerce_payment_db` -
`20260923170926_AddRefunds`; `ecommerce_inventory_db` - no migration

**Testing**: xUnit against real PostgreSQL in three services (`CancellationTests`, `RestockTests`,
`AnnouncementTests`, `RefundTests`); Vitest; Bruno; `verify-saga.sh` (the only check that sees across
services); a mutation run per service

**Target Platform**: Order 5059, Inventory 5060, Payment 5061, RabbitMQ; the gateway on 5000

**Constraints**: cancel and ship serialise per order; each effect exactly once under redelivery; no new
enum value in any service; consumer class names unique across services (queue names)

**Scale/Scope**: a cancellation is a person's action; one event per cancelled order

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Each service undoes its own part from one event; no service reads another's data. |
| II - Clean Architecture | **Pass.** Commands in Application; SQL and locks in Infrastructure; consumers in WebApi. |
| III - Atomic writes, idempotent messaging | **Pass.** Order: status + outbox message in one transaction under the row lock. Inventory: status-guarded under FOR UPDATE. Payment: unique `OrderId` on refunds. |
| IV - Identity from the token | **Pass.** Owner and "who cancelled" from the token; no user id in any body. |
| V - Evidence | **Pass.** Tests for each refusal, the cancel/ship race, idempotency in all three services, payouts excluding cancelled; verify-saga across services. |
| Schema compatibility | **Pass.** Additive only; no enum value an older image cannot parse (research D3). |

No Complexity Tracking entries.

**Post-design re-check**: unchanged. Principle III is where the design is load-bearing, and the code
keeps it: the event is staged by a callback **inside** `TryCancelAsync`'s transaction, after the guarded
`UPDATE` and before the one `SaveChangesAsync`, so a refused or repeated cancellation publishes nothing;
the transaction runs in `CreateExecutionStrategy()` with the change tracker cleared first, so a retried
attempt cannot save a staged message twice. Two tests did not catch their guard at first (a concurrent
refund that never raced, a client string) and were fixed rather than the guard being declared covered.

## Project Structure

### Documentation (this feature)

```text
specs/039-order-cancellation/
├── spec.md
├── plan.md                  # this file
├── research.md              # D1-D6
├── data-model.md            # CancelledBy, refunds, the reservation transitions
├── quickstart.md
├── contracts/
│   ├── api.md               # HTTP, and the event in brief
│   └── messages.md          # OrderCancelledEvent: publisher, consumers, idempotency
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull request)

```text
server/src/BuildingBlocks/Ecommerce.Contracts/Order/OrderSubmittedEvent.cs     # OrderCancelledEvent, in this file
server/src/Services/Order/
├── Ecommerce.Order.Domain/Entities/Order.cs                                   # CancelledBy
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/IOrderRepository.cs                                  # TryCancelAsync
│   ├── Orders/Commands/CancelOrder/CancelOrderCommands.cs                     # new: customer + staff
│   ├── Orders/Commands/{Fulfilment/FulfilmentStep.cs,SellerFulfilment/SellerFulfilmentCommands.cs}
│   └── Orders/Common/{Cancellation.cs,OrderMapping.cs,OrderResponses.cs,SaleResponses.cs,ShipmentMove.cs}
├── Ecommerce.Order.Infrastructure/
│   ├── Migrations/20260923165912_AddOrderCancelledBy.cs
│   ├── Persistence/Configurations/OrderConfiguration.cs
│   └── Persistence/Repositories/{OrderRepository.cs,PayoutRepository.cs}     # Sales.Earning
└── Ecommerce.Order.WebApi/Controllers/OrdersController.cs                    # two routes
server/src/Services/Inventory/
├── Ecommerce.Inventory.Application/Reservations/RestockCancelledOrder/       # command + handler, new
├── Ecommerce.Inventory.Application/Common/StockAvailabilityAnnouncer.cs
└── Ecommerce.Inventory.WebApi/{Consumers/RestockCancelledOrderConsumer.cs,Program.cs}
server/src/Services/Payment/
├── Ecommerce.Payment.Domain/Entities/Refund.cs                               # new
├── Ecommerce.Payment.Application/Payments/RefundOrder/                       # command + handler, new
├── Ecommerce.Payment.Application/Payments/{Common/PaymentResponse.cs,Queries/...}
├── Ecommerce.Payment.Infrastructure/{Migrations/20260923170926_AddRefunds.cs,Persistence/...}
└── Ecommerce.Payment.WebApi/{Consumers/RefundCancelledOrderConsumer.cs,Program.cs}
server/tests/Ecommerce.Order.Tests/CancellationTests.cs                        # new
server/tests/Ecommerce.Inventory.Tests/{RestockTests.cs,AnnouncementTests.cs}
server/tests/Ecommerce.Payment.Tests/{RefundTests.cs,ConcurrentInsertRecoveryTests.cs}
client/src/components/order/cancel-order/index.tsx                              # new
client/src/utils/order/{cancel.ts,cancel.test.ts}                               # when the button is drawn
client/src/pages/{order,admin-order,shop-sale}/..., shop-sales/status.ts, components/seller/sale-earnings/
client/src/services/{order,admin}/, hooks/{order,admin}/, constants/order/, locales/{en,vi}/{orders,admin,seller}.json
bruno/admin-audit/cancelling a shipped order is 409 for {its customer,staff}.yml
bruno/security-checks/cancelling an order without a token is 401.yml
bruno/seller/cancelling someone else s order is 404.yml
.github/scripts/verify-saga.sh                                                  # a cancellation scenario
CLAUDE.md, .specify/feature.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |

## Verification

**What the pull request recorded** (#84): server **405** tests - Order 152 (+12), Inventory 44 (+6),
Payment 18 (+5). The Inventory and Payment tests were seen red against no-op handlers first (5 and 2 red);
Order's code preceded its tests, so every guard was removed one at a time - **Order 10/10, Inventory 3/3,
Payment 3/3, client 6/6** each turned a test red. Client **159** (+14), lint clean, build passing. Bruno
**118/118** requests, **188/188** tests. `verify-saga.sh` cancels a second order: stock back to **47/0**
as before the order, a refund of **1,155,000 of 1,155,000**, a repeat answered 200; both new queues had
exactly one consumer. In a browser Lan cancelled an order; the dialog closed and the page read "Bạn đã huỷ
đơn này. Số tiền đã trả được hoàn lại."

## What this feature does not finish

- Cancelling one parcel of a multi-seller order.
- Returns after delivery - specs/066.
- A customer's reason, or a fee.
- Moving money: the refund is a ledger entry through the stub provider.
