# Implementation Plan: Cancelling a paid order

**Branch**: `039-order-cancellation` | **Spec**: [spec.md](spec.md) | [research](research.md) |
[contracts](contracts/api.md)

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

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Each service undoes its own part from one event; no service reads another's data. |
| II - Clean Architecture | Commands in Application; SQL and locks in Infrastructure; consumers in WebApi. |
| III - Atomic writes, idempotent messaging | Order: status + outbox message in one transaction under the row lock. Inventory: status-guarded under FOR UPDATE. Payment: unique `OrderId` on refunds. |
| IV - Identity from the token | Owner and "who cancelled" from the token; no user id in any body. |
| V - Evidence | Tests for each refusal, the cancel/ship race, idempotency in all three services, payouts excluding cancelled; verify-saga across services. |
| Schema compatibility | Additive only; no enum value an older image cannot parse (research D3). |

No Complexity Tracking entries.
