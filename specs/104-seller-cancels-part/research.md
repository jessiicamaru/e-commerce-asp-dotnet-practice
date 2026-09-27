# Research: A seller cancels the part of an order they cannot fulfil

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #211

---

## D1 - A cancelled part is columns, not a status

**Decision**: `order_shipments` gains `CancelledAt`, `CancelReason`, `CancelledBy` ("Seller" / "Staff") and
`CancelRefund`. The part keeps the `Status` it had (`Pending` or `Preparing`).

**Rationale**: A new `ShipmentStatus` value would stop an image rolled back to before this from reading the row. That
is the rule specs/035 and specs/040 followed ("delivered" is columns too). `CancelRefund` stores what the part
refunded, so insights can subtract it the way they subtract a received return's `RefundAmount` (specs/084), without
recomputing it later from lines whose discounts might change meaning.

**Alternatives considered**: a `Cancelled` shipment status, rejected for the rollback reason above; and deleting the
part row, rejected because it would erase who cancelled and why, and the seller's record of the sale.

---

## D2 - What the part refunds

**Decision**: The refund is the part's lines as the buyer paid for them: unit price times quantity, less the line's
shop and platform discounts, plus its tax. This is the same sum a received return refunds (`ReturnFeatures`). Delivery
is not refunded, because it is charged per order and the order still ships. When the part is the last one left, the
order is cancelled whole (US2), and the refund is what is left of the charge, delivery included.

**Rationale**: The buyer should get back exactly what those goods cost them. Using the same formula as returns keeps
the two ways a part stops being a sale consistent. Splitting delivery by part would be refunding a share of a service
the buyer still gets.

**Alternatives considered**: refunding the part's `ShippingShare`, rejected because that share is a seller's
earnings split (specs/037), not what the buyer paid per parcel, and under free delivery the buyer paid none.

---

## D3 - Inventory decides from reservations, filtered to the part

**Decision**: `OrderPartCancelledEvent` carries the part's variant ids. Inventory runs the specs/039 restock
(`RestockCancelledOrderCommand`) limited to those variants: a `Held` reservation is released, a `Confirmed` one goes
back on hand, and both end `Released`. The consumer is `RestockCancelledPartConsumer`.

**Rationale**: The order is `Paid` in Order before Inventory has necessarily confirmed, because the two consume
`OrderCompletedEvent` independently. The return path (`RestockReturnedParcel`) assumes the units were confirmed long
ago and would count held units twice. The reservation path already handles both states, and each variant's reservation
belongs to one seller's part (the Assumption in the spec). The guard is the status each row moves from, so a
redelivery moves nothing, and a later whole-order cancellation skips the rows already released.

---

## D4 - Payment owns "what is left"

**Decision**: `refunds.PartId` (nullable) is unique where not null. The whole-order unique index becomes
`"ReturnId" IS NULL AND "PartId" IS NULL`. `RefundOrder` refunds the payment's amount less the sum of that order's
part refunds.

**Rationale**: Payment decides amounts from its own rows (specs/039: the event "carries no amount"). A part refund is
the one case where Order supplies the amount, because only Order knows the lines. Without the subtraction, the buyer
who later cancels the rest would be refunded the cancelled part twice. This is the defect the design has to rule out
(SC-006).

---

## D5 - Vouchers: only the cancelled seller's own comes back

**Decision**: A part cancellation (not the last) gives back the use of any voucher on the order whose `SellerId` is
the part's seller. The shop's own part releases nothing. The last part releases every voucher, through the
whole-order path.

**Rationale**: A shop voucher discounted that seller's lines only (specs/069), so it bought nothing once they are
gone. A platform voucher and free delivery applied to the whole order, which goes on.

---

## D6 - One lock, the parcel moves' lock

**Decision**: The cancel takes `SELECT ... FROM orders WHERE "Id" = @id FOR UPDATE` first, like every move and the
whole-order cancel.

**Rationale**: A seller cancelling while staff ship, or two sellers cancelling the last two parts at once, must
serialise. Otherwise both could see "another part remains", and nobody would cancel the order
(`ShipmentTests`-style race). SC-006 removes the lock and expects a test to fail.

---

## D7 - Who, and the notice

**Decision**: The seller cancels their own part, and an administrator cancels the shop's part. A reason is required,
because the buyer reads it. The buyer gets `PartCancelled` in the app. There is no email in this feature.

**Alternatives considered**: letting moderators cancel, rejected because fulfilment is Admin-only (specs/039); and
emailing the buyer, deferred, because a new email template brings its own editor labels and tests (specs/083) and is
recorded as out of scope.

---

## D8 - Deploy order

**Decision**: Deploy Inventory and Payment (the new consumers) before Order (the publisher).

**Rationale**: MassTransit publishes to an exchange. A message published before any queue is bound to it is dropped.
Recorded in `quickstart.md` and the PR.
