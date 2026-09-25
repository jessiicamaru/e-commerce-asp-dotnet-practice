# Feature Specification: Returning a delivered parcel (part 2 - the screens)

**Feature Branch**: `067-return-screens` | **Created**: 2026-09-25 | **Issue**: #107 (closes it)
**Builds on**: [specs/066-parcel-returns](../066-parcel-returns/) - the server, merged in #149.

## Why

Since #149 the server can take a delivered parcel back, refund it and restock it, but nobody can reach any of
it: no page draws a return and no button starts one. The returns exist only in Bruno. This part is the
storefront.

## User Scenarios

### US1 - The buyer returns a parcel (P1)

A buyer opens their order.
- A parcel delivered less than 7 days ago offers **Return this parcel**. A dialog asks why, and the request
  is sent once it is confirmed.
- The parcel then shows where its return has got to, in words:
  - waiting for the seller;
  - accepted: send it back by a date, with a form for the tracking reference;
  - refused, with the seller's reason, and **Ask the shop to look again** while that is still possible;
  - with staff;
  - turned down for good, with the reason;
  - on its way back;
  - returned, with the amount refunded.

**Acceptance**:
1. A parcel delivered today offers the return. One delivered 8 days ago does not, and neither does one not
   yet delivered.
2. Sending a request needs a reason, and it calls the server with the order, the parcel and the reason.
3. An accepted return shows the form. Sending it calls the server with the tracking reference.
4. A refused return shows the reason and offers escalation. After the escalation window, it offers nothing.
5. A server refusal (a 409) is shown in the server's words.

### US2 - The seller answers (P1)

On a sale with a return:
- the seller sees the buyer's reason;
- while the return is requested they can **Accept** or **Refuse**, and a refusal needs a reason;
- once the parcel is sent back they can **Mark as received**. This is confirmed first, because it refunds
  the buyer and puts the units back.

**Acceptance**: each button calls its sale endpoint. A refusal without a reason cannot be sent. Received is
offered only for `SentBack`, and the decision only for `Requested`.

### US3 - Staff (P2)

- **An administrator's order page** has a returns card, listing every parcel of the order that has a return.
  - For the shop's own parcel, staff decide and receive it, the way a seller would.
  - For an escalated parcel of anybody's, staff give **the final word**: accept, or reject for good.
  - A seller's parcel that is not escalated shows its state with no buttons.
- **`/admin/returns`** is the queue: escalated (the default), requested, sent back and received, oldest
  first, each row linking to its order. It is Admin only, like the endpoint.

**Acceptance**: the staff route is called with the order and the parcel. An escalated refusal says it is
final. The queue asks for the state in its address.

### Edge cases

- A parcel's return state comes from the server, which also refuses on its own. The pages only decide what
  to *offer*, using the same rules and the same 7 days (research D1).
- A cancelled order draws no parcels, so no return is offered.
- Confirming a parcel used to "release the seller's payment". Since specs/066 it starts the return window
  instead, and the confirmation dialog must stop saying the old thing.

## Requirements

- **FR-001** Each parcel on the buyer's order page shows its return, and offers only the next step the server
  would accept.
- **FR-002** The seller's sale page shows the return and the seller's two decisions and the receipt.
- **FR-003** The administrator's order page shows every return of the order, with staff's steps.
- **FR-004** `/admin/returns` lists returns by state, and appears in the console menu for administrators.
- **FR-005** Every sentence is in Vietnamese and English.
- **FR-006** Tests (Vitest) cover:
  - what each role is offered, in each state;
  - what each action sends;
  - the window;
  - a refusal from the server shown in its own words.

## Out of scope

- Photographs of the damage, partial returns and return shipping labels.
- A badge on the sales list. The seller is told through the `ReturnRequested` notice, which links to the sale.
