# Research: Cancelling a paid order

## D1 - Order decides, then tells: one new event

**Decision**: Order moves the row to `Cancelled` and publishes `OrderCancelledEvent(OrderId,
CancelledAt, CancelledBy)` through its outbox in the same transaction. Inventory and Payment consume it.
The Orchestrator is not involved: the saga ended at payment, and cancellation is a decision Order owns,
like fulfilment - choreography after the saga, not a second saga.

The event carries no items or amount. Inventory knows what it reserved for the order (its reservation
rows), and Payment knows what it charged (its payment row); sending either again would be a second
opinion they could disagree with.

## D2 - The order's row lock serialises cancel against ship

`TryCancelAsync` takes the same `SELECT … FOR UPDATE` on the order as every parcel move (specs/035 D5),
then reads every part and decides. So a cancel and a ship on one order run one after the other: a
shipped part is seen and the cancel refused, or the cancel commits first and the move then finds an
order that is not payable. Parts an older image never wrote are created first, as for moves.

## D3 - No new status anywhere a rolled-back image must parse

- Order: `Cancelled` has been in the enum since feature 003 - an older image parses it.
- Inventory: returned stock does **not** get a new `ReservationStatus`. A confirmed reservation that is
  put back moves to `Released`, with `SettlementReason` "Returned: order cancelled"; the guard is the
  status it moves from, `Confirmed`.
- Payment: `payments` keeps one row per order (unique `OrderId`) and is immutable; the refund is a new
  table, `refunds`, unique on `OrderId`.
- Order: one nullable column, `CancelledBy`.

## D4 - Inventory handles the race with completion

`OrderCompletedEvent` and `OrderCancelledEvent` can reach Inventory in either order. The restock
handler, under `FOR UPDATE` on the stock rows:
- `Confirmed` reservations: `QuantityOnHand += qty`, → `Released`.
- `Held` reservations (completion not arrived yet): `QuantityReserved -= qty`, → `Released`.

Either way, a late `OrderCompletedEvent` then finds no `Held` rows and deducts nothing. It announces
availability, as every stock-moving handler must.

## D5 - Sellers see a cancelled sale; money never does

`Sales.Statuses` (what a seller sees) gains `Cancelled`, so a seller mid-preparation learns to stop.
A new `Sales.Earning` (Paid, Completed, Preparing, Shipped) is what balances and payouts count. A
seller's cancelled sale shows no address and no step; moving it is 409 "This order was cancelled."
- but only to a seller who has a part on it; anyone else gets the usual 404.

## D6 - Consumer names are unique across services

`RestockCancelledOrderConsumer` (Inventory) and `RefundCancelledOrderConsumer` (Payment). Neither
service sets an endpoint name prefix, so two classes of the same name would share one queue and each
event would reach only one of them (CLAUDE.md gotcha).
