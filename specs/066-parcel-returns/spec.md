# Feature Specification: Returning a delivered parcel

**Feature Branch**: `066-parcel-returns` | **Created**: 2026-09-25 | **Issue**: #107

## Why

Once a parcel has arrived there is nothing a customer can do: a broken camera, a wrong lens or a
counterfeit cannot be sent back or refunded. A paid order can be cancelled only while every parcel still
waits (specs/039).

## User Scenarios

### US1 - Ask to return a parcel (P1)

**Acceptance**
1. The buyer asks to return a **whole delivered parcel** with a reason
   (`POST /api/orders/{id}/shipments/{shipmentId}/return`).
2. They may ask only within **`Returns:WindowDays` (7) of its delivery**, and only once per parcel.
   - Someone else's order is 404.
   - A parcel not delivered, or past its window, is 409.
   - A second request is 409.
3. The parcel's seller is told (`ReturnRequested`). The shop's own parcels are answered by an
   administrator.

### US2 - The seller decides; a refusal can be disputed (P1)

**Acceptance**
1. The seller of the parcel (an administrator for the shop's parcel) **accepts** it or **refuses** it with
   a reason. The buyer is told either way.
2. A refused return can be **escalated** by the buyer within the window of the refusal. An
   **administrator** then accepts it or rejects it for good. The queue is `GET /api/orders/returns`.
3. Every decision is one guarded statement from the state it expects. A second or late decision is 409
   and changes nothing.

### US3 - Sent back, received, refunded, restocked (P1)

**Acceptance**
1. Once accepted, the buyer records the return's tracking reference (`.../return/sent`) within the window.
   The seller is told.
2. The seller (an administrator for the shop's parcel) marks it **received**. In that transaction Order
   publishes `ParcelReturnedEvent` with the parcel's lines and the refund: **goods plus their tax, not
   delivery**.
3. **Payment** records one refund for the return (a ledger entry; Payment is still a stub). **Inventory**
   puts the units back **once**. The buyer is told.

### US4 - Money: a hold, never a debt (P1)

**Acceptance**
1. A seller's part becomes **due** only once it was delivered **more than the window ago** and no return
   of it is open. Open means one of:
   - a request, an escalation, or an item on its way back;
   - an accepted or refused return that is still inside its window.
2. A **returned** part is never money: not on the way, not due, never paid out.
3. A return can start only inside the window, and money is due only after it. So **nothing already paid
   out is ever returned**, and there is no debt.

## Out of scope

- Returning part of a parcel, and photos. Both are recorded as out of scope.
- The storefront screens, which are part 2 of #107. This part carries only the words for the new notices,
  which the notification contract requires.
