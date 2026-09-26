# Research: Cancelling a paid order

> Completed on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Decisions as written before the code; labels added so each
> reads like specs/001. The window and whole-order-only were decided with the user (spec.md).

**Feature**: [spec.md](spec.md)

## D1 - Order decides, then tells: one new event

**Decision**: Order moves the row to `Cancelled` and publishes `OrderCancelledEvent(OrderId,
CancelledAt, CancelledBy)` through its outbox in the same transaction. Inventory and Payment consume it.
The Orchestrator is not involved: the saga ended at payment, and cancellation is a decision Order owns,
like fulfilment - choreography after the saga, not a second saga.

**Rationale**: The event carries no items or amount. Inventory knows what it reserved for the order (its reservation
rows), and Payment knows what it charged (its payment row); sending either again would be a second
opinion they could disagree with.

**Alternatives considered**: involving the Orchestrator (a second saga) - rejected above, the saga ended
at payment; putting items and the amount on the event - rejected above.

## D2 - The order's row lock serialises cancel against ship

**Decision**: cancel takes the order's row lock, the same one every parcel move takes.

**Rationale**: `TryCancelAsync` takes the same `SELECT … FOR UPDATE` on the order as every parcel move (specs/035 D5),
then reads every part and decides. So a cancel and a ship on one order run one after the other: a
shipped part is seen and the cancel refused, or the cancel commits first and the move then finds an
order that is not payable. Parts an older image never wrote are created first, as for moves.

As built, `Cancel_and_ship_at_once_leave_exactly_one_winner` exercises it, and removing the lock turned a
test red in the mutation run. The transaction runs in `CreateExecutionStrategy()` and clears the change
tracker first, so a retried attempt does not save a staged outbox message twice.

**Alternatives considered**: none recorded.

## D3 - No new status anywhere a rolled-back image must parse

**Decision and rationale**, per service:

- Order: `Cancelled` has been in the enum since feature 003 - an older image parses it.
- Inventory: returned stock does **not** get a new `ReservationStatus`. A confirmed reservation that is
  put back moves to `Released`, with `SettlementReason` "Returned: order cancelled"; the guard is the
  status it moves from, `Confirmed`.
- Payment: `payments` keeps one row per order (unique `OrderId`) and is immutable; the refund is a new
  table, `refunds`, unique on `OrderId`.
- Order: one nullable column, `CancelledBy`.

**Alternatives considered**: a `Returned` reservation status or a `Refunded` payment status - rejected
above, an older image could not parse them.

## D4 - Inventory handles the race with completion

**Decision**: one restock handler that settles both `Confirmed` and `Held` rows for the order.

**Rationale**: `OrderCompletedEvent` and `OrderCancelledEvent` can reach Inventory in either order. The restock
handler, under `FOR UPDATE` on the stock rows:
- `Confirmed` reservations: `QuantityOnHand += qty`, → `Released`.
- `Held` reservations (completion not arrived yet): `QuantityReserved -= qty`, → `Released`.

Either way, a late `OrderCompletedEvent` then finds no `Held` rows and deducts nothing. It announces
availability, as every stock-moving handler must.

As built it takes `GetForUpdateAsync` on the stock rows in the same ascending order as every other stock
path, and `AnnouncementTests.RestockCancelledOrder_announces_the_units_put_back` holds it to announcing.

**Alternatives considered**: none recorded. The design accepts either order because nothing orders
delivery across message types (#15).

## D5 - Sellers see a cancelled sale; money never does

**Decision**: split what a seller sees from what is money.

**Rationale**: `Sales.Statuses` (what a seller sees) gains `Cancelled`, so a seller mid-preparation learns to stop.
A new `Sales.Earning` (Paid, Completed, Preparing, Shipped) is what balances and payouts count. A
seller's cancelled sale shows no address and no step; moving it is 409 "This order was cancelled."
- but only to a seller who has a part on it; anyone else gets the usual 404.

**Alternatives considered**: leaving `Cancelled` out of `Sales.Statuses` - rejected, a seller mid-preparation
would never learn to stop; adding it to one list used for both - rejected, it would pay sellers for
cancelled orders.

## D6 - Consumer names are unique across services

**Decision**: name each consumer for what it does.

**Rationale**: `RestockCancelledOrderConsumer` (Inventory) and `RefundCancelledOrderConsumer` (Payment). Neither
service sets an endpoint name prefix, so two classes of the same name would share one queue and each
event would reach only one of them (CLAUDE.md gotcha).

The pull request confirmed on the running stack that both queues had exactly one consumer.

**Alternatives considered**: two classes named `OrderCancelledConsumer` - rejected above, they would share
one queue.
