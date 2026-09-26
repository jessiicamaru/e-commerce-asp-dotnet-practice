# Feature Specification: Returning a delivered parcel (part 2 - the screens)

> Completed on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature Branch**: `067-return-screens` | **Created**: 2026-09-25 | **Issue**: #107 (closes it)
**Builds on**: [specs/066-parcel-returns](../066-parcel-returns/) - the server, merged in #149.

**Status**: Merged (#151, 2026-09-25). Client only.

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

**Why this priority**: The buyer starts every return. Without this page the server's flow from #149 is unreachable by
the person it exists for.

**Independent Test**: Render the order page with a parcel delivered today and one delivered 8 days ago; only the first
offers the return, the dialog refuses an empty reason, and a sent request calls
`POST /orders/{id}/shipments/{shipmentId}/return` with the trimmed reason (`pages/order/index.test.tsx`).

**Acceptance Scenarios**:

1. **Given** a parcel delivered today, **When** the buyer opens their order, **Then** "Return this parcel" is offered
   with the last day shown.
2. **Given** a parcel delivered exactly 7 days ago, **When** the page is drawn, **Then** no return is offered - the
   same edge the server applies.
3. **Given** the return dialog with an empty reason, **When** the buyer confirms, **Then** nothing is sent.
4. **Given** an accepted return inside its window, **When** the buyer enters a tracking reference, **Then** the
   server is called with it and the parcel reads "on its way back".
5. **Given** a refused return older than 7 days, or a final rejection, **When** the page is drawn, **Then** nothing is
   offered.
6. **Given** the server answers 409, **When** the buyer sends a request, **Then** the dialog stays open and shows the
   server's words.
7. **Given** a received return, **When** the page is drawn, **Then** it says what was refunded, in the order's
   currency.
8. **Given** an order with several parcels, **When** the buyer returns one, **Then** the request names that parcel and
   no other.

---

### US2 - The seller answers (P1)

On a sale with a return:
- the seller sees the buyer's reason;
- while the return is requested they can **Accept** or **Refuse**, and a refusal needs a reason;
- once the parcel is sent back they can **Mark as received**. This is confirmed first, because it refunds
  the buyer and puts the units back.

**Acceptance**: each button calls its sale endpoint. A refusal without a reason cannot be sent. Received is
offered only for `SentBack`, and the decision only for `Requested`.

**Why this priority**: A request with nobody able to answer it stalls every seller's return; the seller holds the goods
and, by specs/066, their money is held until they answer.

**Independent Test**: Render the sale page with a requested return, refuse it with and without a reason, then with a
sent-back return mark it received through the confirmation (`pages/shop-sale/index.test.tsx`).

**Acceptance Scenarios**:

1. **Given** a sale with a requested return, **When** the seller opens it, **Then** the buyer's reason and Accept /
   Refuse are shown.
2. **Given** the refuse dialog with no reason, **When** the seller confirms, **Then** nothing is sent.
3. **Given** a return sent back, **When** the seller presses "Mark as received", **Then** a dialog says it refunds the
   buyer, and only on confirmation is `POST /orders/sales/{id}/return/received` called.
4. **Given** an escalated return, **When** the seller opens the sale, **Then** nothing is offered.
5. **Given** a sale without a return, **When** it is drawn, **Then** there is no return card.

---

### US3 - Staff (P2)

- **An administrator's order page** has a returns card, listing every parcel of the order that has a return.
  - For the shop's own parcel, staff decide and receive it, the way a seller would.
  - For an escalated parcel of anybody's, staff give **the final word**: accept, or reject for good.
  - A seller's parcel that is not escalated shows its state with no buttons.
- **`/admin/returns`** is the queue: escalated (the default), requested, sent back and received, oldest
  first, each row linking to its order. It is Admin only, like the endpoint.

**Acceptance**: the staff route is called with the order and the parcel. An escalated refusal says it is
final. The queue asks for the state in its address.

**Why this priority**: P2 because Bruno could already drive the staff steps and the shop's own parcels are few; but a
dispute with no screen is a dispute nobody settles.

**Independent Test**: Render `/admin/returns` - it opens on `Escalated` and asks for another state when a tab is chosen;
render an administrator's order with an escalated seller's return and reject it for good
(`pages/admin-returns`, `pages/admin-order` tests).

**Acceptance Scenarios**:

1. **Given** the shop's own parcel with a requested return, **When** staff accept it, **Then**
   `POST /orders/fulfilment/{id}/shipments/{shipmentId}/return/accept` is called.
2. **Given** an escalated return of a seller's parcel, **When** staff refuse it, **Then** the button reads "Reject for
   good" and a reason is required.
3. **Given** a seller's parcel with a return that is not escalated, **When** staff open the order, **Then** its state is
   drawn with nothing to press.
4. **Given** `/admin/returns?status=Nonsense`, **When** the page loads, **Then** it ignores the address and shows the
   escalated tab.
5. **Given** a moderator, **When** the console menu is drawn, **Then** "Returns" is not in it.

### Edge cases

- A parcel's return state comes from the server, which also refuses on its own. The pages only decide what
  to *offer*, using the same rules and the same 7 days (research D1).
- A cancelled order draws no parcels, so no return is offered.
- Confirming a parcel used to "release the seller's payment". Since specs/066 it starts the return window
  instead, and the confirmation dialog must stop saying the old thing.
- An accepted return whose window has passed says so ("the time to send it back has passed") rather than showing a form
  the server would refuse.
- The queue shows no amounts: a return carries no currency (research D3).

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
- **FR-007** Steps that cannot be taken back - accepting, and marking received - are confirmed in a dialog first.
- **FR-008** No page sends the caller's id; the server decides whose parcel it is.

### Key Entities

- **Parcel return (as drawn)**: the server's `ReturnResponse`, typed `ParcelReturn` in the client, hanging on a parcel
  (`Shipment.return`) or a sale (`Sale.return`).
- **Return queue page**: one state's returns, oldest first, with paging.

## Success Criteria

- **SC-001**: Every step of the return flow from specs/066 can be taken from a page by the party entitled to it - no
  step needs Bruno. Verified by Bruno run through the storefront's nginx (`baseUrl=http://localhost:8088`), 215/215,
  and by the Vitest tests; clicking through in a real browser was not done.
- **SC-002**: No button is offered that the server would refuse because of state or window - each guard copied in
  `utils/order/returns.ts` and killed by at least one test (9 of 9 mutations caught).
- **SC-003**: Every new sentence exists in both languages.
- **SC-004**: The storefront suite stays green: 371/371 in 61 files at merge.

## Assumptions

- The window stays 7 days; the client copies it as a constant for drawing only (research D1).
- The server's 409 messages are readable enough to show as they are.
- The console's existing admin-only menu mechanism (`adminOnly`) is what hides "Returns" from moderators.

## Out of scope

- Photographs of the damage, partial returns and return shipping labels.
- A badge on the sales list. The seller is told through the `ReturnRequested` notice, which links to the sale.
- Any server change - no endpoint, message or table.
