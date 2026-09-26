# Feature Specification: Returning a delivered parcel

> Completed on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature Branch**: `066-parcel-returns` | **Created**: 2026-09-25 | **Issue**: #107

**Status**: Merged (#149, 2026-09-25) - part 1 of #107, the server. Part 2, the screens, is
[specs/067](../067-return-screens/).

**Input**: Issue #107 - a customer whose parcel has arrived broken, wrong or counterfeit has no way to send it back
and be refunded.

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

**Why this priority**: Every other story starts from a request. Without it nothing else in the flow is reachable,
and the defect in #107 - no way back at all once a parcel has arrived - stays exactly as it was.

**Independent Test**: Deliver a parcel, ask to return it as its buyer, and read the order back: the parcel carries a
return in `Requested`, and its seller has a `ReturnRequested` notice. Ask again, ask as somebody else, ask for a parcel
not yet delivered: 409, 404, 409.

**Acceptance Scenarios**:

1. **Given** a parcel delivered 2 days ago, **When** its buyer asks to return it with a reason, **Then** a return in
   `Requested` is recorded against that parcel and its seller is told.
2. **Given** a parcel delivered more than 7 days ago, or one not delivered yet, **When** its buyer asks to return it,
   **Then** the request is refused with 409 and nothing is written.
3. **Given** another customer's order, **When** a customer asks to return one of its parcels, **Then** the answer is
   404, the same as for an order that does not exist.
4. **Given** two requests for the same parcel sent at once, **When** both are processed, **Then** exactly one return
   exists and the other request gets 409.
5. **Given** a request with no reason, **When** it is sent, **Then** it is refused with 400.

---

### US2 - The seller decides; a refusal can be disputed (P1)

**Acceptance**
1. The seller of the parcel (an administrator for the shop's parcel) **accepts** it or **refuses** it with
   a reason. The buyer is told either way.
2. A refused return can be **escalated** by the buyer within the window of the refusal. An
   **administrator** then accepts it or rejects it for good. The queue is `GET /api/orders/returns`.
3. Every decision is one guarded statement from the state it expects. A second or late decision is 409
   and changes nothing.

**Why this priority**: A request nobody can answer is a complaint box, not a return. The dispute path is in the same
story because a refusal without appeal would leave the buyer at the seller's mercy, which is the thing a marketplace
exists to prevent.

**Independent Test**: With a return requested, accept it as the parcel's seller and read the buyer's notices; on a
second parcel refuse it with a reason, escalate it as the buyer, and have an administrator reject it for good.

**Acceptance Scenarios**:

1. **Given** a requested return of a seller's parcel, **When** that seller accepts it, **Then** it is `Accepted` and
   the buyer is told.
2. **Given** a requested return, **When** the seller refuses it without a reason, **Then** 400; with a reason,
   **Then** it is `Refused` and the buyer reads the reason.
3. **Given** a requested return of seller A's parcel, **When** seller B or a customer tries to decide it, **Then** a
   seller gets 404 (the sale is not theirs) and a customer 403.
4. **Given** a requested return of a seller's parcel, **When** an administrator tries to decide it before it is
   escalated, **Then** 409 - a seller answers their own parcel.
5. **Given** a refused return, **When** the buyer escalates it within 7 days of the refusal, **Then** it is
   `Escalated`, the seller can no longer decide it, and an administrator either accepts it or rejects it for good
   (`Rejected`).
6. **Given** a refusal more than 7 days old, **When** the buyer escalates it, **Then** 409.
7. **Given** a return already decided, **When** a second decision arrives, **Then** 409 and nothing changes.

---

### US3 - Sent back, received, refunded, restocked (P1)

**Acceptance**
1. Once accepted, the buyer records the return's tracking reference (`.../return/sent`) within the window.
   The seller is told.
2. The seller (an administrator for the shop's parcel) marks it **received**. In that transaction Order
   publishes `ParcelReturnedEvent` with the parcel's lines and the refund: **goods plus their tax, not
   delivery**.
3. **Payment** records one refund for the return (a ledger entry; Payment is still a stub). **Inventory**
   puts the units back **once**. The buyer is told.

**Why this priority**: This is where the buyer gets their money and the seller their goods. A return that ends at
"accepted" has changed nothing either of them cares about.

**Independent Test**: Take an accepted return through `sent` and `received`; check the return reads `Received` with a
refund amount, Payment has one refund row carrying that `ReturnId`, and the variant's `QuantityOnHand` rose by the
parcel's quantity. Deliver the event again and check neither moved a second time.

**Acceptance Scenarios**:

1. **Given** an accepted return, **When** the buyer records a tracking reference within 7 days of the acceptance,
   **Then** it is `SentBack` and the seller is told the reference.
2. **Given** an accepted return more than 7 days old, **When** the buyer tries to send it back, **Then** 409 - the
   acceptance has lapsed.
3. **Given** a return on its way back, **When** its seller marks it received, **Then** it is `Received` with
   `RefundAmount` = the parcel's goods plus their tax, and `ParcelReturnedEvent` is published in the same transaction.
4. **Given** that event, **When** Payment and Inventory consume it, **Then** one refund is recorded and the units are
   back on hand - and a redelivery, or two deliveries at once, changes neither.
5. **Given** a return already received, **When** "received" is sent again, **Then** 409.
6. **Given** a seller's parcel, **When** an administrator marks it received, **Then** 409 - it goes back to its
   seller, who marks it.

---

### US4 - Money: a hold, never a debt (P1)

**Acceptance**
1. A seller's part becomes **due** only once it was delivered **more than the window ago** and no return
   of it is open. Open means one of:
   - a request, an escalation, or an item on its way back;
   - an accepted or refused return that is still inside its window.
2. A **returned** part is never money: not on the way, not due, never paid out.
3. A return can start only inside the window, and money is due only after it. So **nothing already paid
   out is ever returned**, and there is no debt.

**Why this priority**: Without it a seller can be paid out for a parcel that later comes back, and the shop then owns a
debt it has no way to collect. It is P1 because money is the part that cannot be put right afterwards.

**Independent Test**: Deliver a seller's parcel 8 days ago with a return open; the balance shows it on the way, not
due, and the payout claim takes nothing. Close the return and the part becomes due; receive it and it disappears from
every figure.

**Acceptance Scenarios**:

1. **Given** a seller's parcel delivered 3 days ago, **When** the balance is read, **Then** its money is on the way,
   not due.
2. **Given** a parcel delivered 8 days ago with a return `Requested`, `Escalated` or `SentBack`, **When** the balance
   is read or a payout is recorded, **Then** its money is held - not due, not claimed.
3. **Given** a parcel whose return was finally `Rejected`, or refused more than 7 days ago and never escalated,
   **When** the balance is read, **Then** its money is due again.
4. **Given** a parcel whose return was `Received`, **When** the balance is read, **Then** the part counts nowhere -
   not on the way, not due, not paid out.
5. **Given** any mix of parts, **When** an administrator records a payout, **Then** it claims exactly what the balance
   calls due.

### Edge Cases

- **Two requests at once for one parcel.** One row; the second is 409 (`ON CONFLICT ("ShipmentId") DO NOTHING`).
- **A decision racing a decision.** Both run a guarded `UPDATE ... WHERE "Status" = @from`; one affects a row, the
  other affects none and answers 409.
- **The window's edge.** A parcel delivered exactly 7 days ago is past its window: the request checks
  `DeliveredAt <= now - window`.
- **A seller who never answers.** Their money for the parcel stays held; nothing lapses a `Requested` return. That is
  the incentive to answer.
- **An accepted return never sent back, a refusal never escalated.** Both stop holding the money once their window
  passes; the buyer's step is then 409.
- **A cancelled order.** Its parcels were never delivered; a request is 409.
- **A variant deleted since the sale.** Inventory has no stock row for it; the units are logged as not put back rather
  than a row invented.
- **A refund beyond what was paid, or in another currency.** Payment refuses it and logs an error rather than record
  it.
- **Two parcels of one order returned.** Two refunds, two restocks - keyed by the return, not by the order.
- **A return refund beside a whole-order cancellation refund.** Both can exist; they are told apart by `ReturnId`.
- **An order line from before variants.** The line's `ProductId` stands in for the variant, which is what Inventory's
  first variant reuses (specs/020).

## Requirements

### Functional Requirements

- **FR-001**: The buyer of an order MUST be able to ask to return a whole delivered parcel of it, with a reason, within
  the configured window (`Returns:WindowDays`, default 7) of the parcel's delivery.
- **FR-002**: A parcel MUST have at most one return, whatever the timing of the requests.
- **FR-003**: Someone else's order MUST answer as not found, indistinguishable from a missing one.
- **FR-004**: The parcel's seller MUST be able to accept or refuse a requested return; a refusal MUST carry a reason
  the buyer can read. For the shop's own parcel an administrator does both.
- **FR-005**: The buyer MUST be able to escalate a refusal within the window of the refusal; an administrator then
  accepts it or rejects it finally. A seller MUST NOT decide an escalated return.
- **FR-006**: The buyer MUST be able to record the tracking reference of an accepted return within the window of the
  acceptance.
- **FR-007**: The parcel's seller (an administrator for the shop's parcel) MUST be able to mark a returned parcel
  received, and that MUST, in the same transaction, announce the parcel's lines and the refund amount.
- **FR-008**: The refund MUST be the parcel's goods plus their tax, as frozen on the order at checkout; the delivery
  charge MUST NOT be refunded.
- **FR-009**: Payment MUST record exactly one refund per return, never more in total than was paid for the order and
  never in another currency.
- **FR-010**: Inventory MUST put a returned parcel's units back on hand exactly once, and announce the new
  availability like every other path that moves stock.
- **FR-011**: Every step MUST move the return only from the state it expects; a second or late step MUST change
  nothing and answer 409.
- **FR-012**: A seller's part MUST become due only once delivered more than the window ago with no return of it open;
  a returned part MUST count as no money at all.
- **FR-013**: The payout claim MUST take exactly what the balance calls due.
- **FR-014**: Each step MUST be recorded in the audit log and MUST tell the other party (`ReturnRequested`,
  `ReturnAccepted`, `ReturnRefused`, `ReturnSentBack`, `ReturnRefunded`).
- **FR-015**: Staff MUST be able to list returns by state, the escalated ones being the dispute queue.
- **FR-016**: A parcel's return MUST appear on the buyer's order and on the seller's sale.

### Key Entities

- **Parcel return**: one buyer's request to send one delivered parcel back - which order and parcel, whose (buyer and
  seller, none for the shop's own), its state, the reasons on each side, the tracking reference, the refund, and when
  each step happened. One per parcel.
- **Refund of a return**: Payment's ledger entry for money given back for one return. At most one per return.
- **Returned parcel claim**: Inventory's record that a return's units were put back. One per return.

## Success Criteria

- **SC-001**: A delivered parcel can be taken from request to refund entirely through the API, by the buyer and the
  party entitled to each step. Verified end to end through the gateway by Bruno (215/215 requests).
- **SC-002**: However many times `ParcelReturnedEvent` is delivered, one refund row and one restock exist for the
  return (`A_returned_parcel_is_refunded_its_amount_once`, `A_return_delivered_many_times_restocks_once`).
- **SC-003**: No seller is ever paid out for a parcel that can still come back: over any mix of parts the payout claims
  exactly what the balance calls due (`The_payout_claims_exactly_what_the_balance_calls_due`).
- **SC-004**: Every second or late step answers 409 and changes nothing - checked by mutation: removing the state guard
  fails three tests.
- **SC-005**: The refund is exactly goods plus tax: a mutation dropping the tax fails
  `Received_announces_the_lines_and_the_refund_of_goods_and_tax_once`.

## Assumptions

- A parcel is delivered when its buyer says so or the 7-day sweep takes it as delivered (specs/040); `DeliveredAt` is
  the date the window counts from.
- Payment is still a stub, so a refund is a ledger entry; nothing models the carrier or the return trip.
- The customer pays for sending the parcel back. Nothing in the system models return shipping.
- Returns are an administrator's job, not a moderator's, because the fulfilment endpoints are `Admin` (specs/039).
- One window length serves the request, the buyer's steps after a decision and the money hold.

## Out of scope

- Returning part of a parcel, and photos. Both are recorded as out of scope.
- The storefront screens, which are part 2 of #107. This part carries only the words for the new notices,
  which the notification contract requires.
- Clawing back money already paid out: the design makes it unnecessary (US4).
- Refunding the delivery charge, or modelling the return trip's cost.
