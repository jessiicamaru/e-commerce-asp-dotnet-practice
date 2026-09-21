# Feature Specification: Somewhere for the Order to Go

**Feature Branch**: `011-order-shipping`

**Created**: 2026-09-21

**Status**: Draft

**Input**: Issue [#20](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/20) — "an order has no destination and nothing is ever shipped"

## Why this exists

An order says what was bought and by whom. It does not say **where it goes**. Nobody can be asked for
a delivery address, nobody can choose between a cheap slow delivery and an expensive fast one, and
once an order is paid there is no record of anything ever being sent. The last thing an order can
say about itself is *Completed*, which means *paid* — a warehouse would call that the beginning.

## Decisions already taken

Three questions that #20 left open were put to the person who owns the project, on 2026-09-21:

- **The address book belongs to the account service (Identity).** It already owns the customer. The
  cost is that it gains a second responsibility beyond signing people in; the alternative — a new
  service for customer profiles — was judged not worth an extra service at this size.
- **Fulfilment is manual.** Checkout still ends at payment. After that, a member of staff moves the
  order to *being prepared* and then *sent*. Making despatch an automatic step of checkout — with a
  simulated courier — was considered and deferred: it turns a transaction measured in seconds into
  one measured in days, and changes what undoing a failure means.
- **After payment the order reads *Paid*, then *Preparing*, then *Shipped*.** Not *Completed*, then
  *Preparing* — which reads backwards. The internal message announcing a successful checkout keeps
  its name; only what the order says about itself changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A customer keeps delivery addresses (Priority: P1)

A signed-in customer saves the addresses they deliver to — home, work, a relative's — marks one as
their default, and edits or removes them.

**Why this priority**: Nothing can be sent anywhere until somewhere exists. Every other story reads
from this one.

**Independent Test**: Save two addresses, mark the second as default, edit the first, delete it,
and list what remains.

**Acceptance Scenarios**:

1. **Given** a signed-in customer, **When** they save an address, **Then** it appears in their
   address list.
2. **Given** a customer with no addresses, **When** they save their first, **Then** it is their
   default without being asked.
3. **Given** several addresses, **When** the customer marks one as default, **Then** it is the only
   default.
4. **Given** an address, **When** the customer edits or deletes it, **Then** the list reflects that.
5. **Given** the customer deletes their default address, **When** other addresses remain, **Then**
   one of them becomes the default — a customer with addresses always has exactly one default.
6. **Given** an address missing a required part, **When** it is saved, **Then** it is refused, and
   the refusal says which part.

---

### User Story 2 - Checkout sends the order somewhere, and charges for sending it (Priority: P1)

At checkout the customer chooses one of their addresses and a delivery option. The order records
where it is going and how, and the amount charged includes delivery.

**Why this priority**: Equal first. An address book nothing reads is a form. And the choice has to
cost money, or the total never stops being "the sum of the lines" — which #21 depends on.

**Independent Test**: Check out choosing an address and express delivery; confirm the order holds
that address and that option, and that the amount charged is the items plus the express price.

**Acceptance Scenarios**:

1. **Given** a cart and a saved address, **When** the customer checks out choosing that address and
   a delivery option, **Then** the order records a copy of the address and the option's name and
   price.
2. **Given** a checkout with express delivery, **When** payment is taken, **Then** the amount is the
   items plus the express price — not the items alone.
3. **Given** at least two delivery options with different prices, **When** the customer asks what
   is available, **Then** they see each option's name and price.
4. **Given** a checkout that names no address, **When** the customer has a default, **Then** the
   default is used; **When** they have none, **Then** checkout is refused and says so.
5. **Given** a checkout naming a delivery option that does not exist, **When** submitted, **Then** it
   is refused.

---

### User Story 3 - What an order was sent to never changes (Priority: P1)

A customer edits or deletes an address after ordering. Orders already placed keep the address they
were placed with.

**Why this priority**: This is the rule #18 established for prices, and it fails silently: an order
that follows the address book would quietly move a delivered parcel to a new house. Nobody would
notice until a dispute.

**Independent Test**: Place an order, then edit and delete the address it used; read the order.

**Acceptance Scenarios**:

1. **Given** a placed order, **When** the address it used is edited, **Then** the order still shows
   the address as it was.
2. **Given** a placed order, **When** the address it used is deleted, **Then** the order is
   unaffected.
3. **Given** a placed order, **When** the delivery option's price later changes, **Then** the order
   keeps the price it was charged.

---

### User Story 4 - Staff move a paid order through fulfilment (Priority: P2)

Staff see paid orders, mark one as being prepared, and then as sent with a tracking reference. The
customer sees each step on their order.

**Why this priority**: Second, because payment already works without it — but without it the
system still cannot say anything was ever sent, which is half of #20.

**Independent Test**: Place and pay an order; as staff, move it to preparing and then shipped with a
tracking reference; as the customer, read it at each step.

**Acceptance Scenarios**:

1. **Given** a successful checkout, **When** the customer reads their order, **Then** it says
   *Paid*.
2. **Given** a paid order, **When** staff mark it as being prepared, **Then** it says *Preparing*.
3. **Given** an order being prepared, **When** staff mark it as sent with a tracking reference,
   **Then** it says *Shipped* and shows the reference.
4. **Given** an order in any state, **When** staff try to move it anywhere but the next step —
   backwards, skipping a step, or from a failed order — **Then** it is refused and the order is
   unchanged.
5. **Given** a move that was already made, **When** it is requested again, **Then** nothing changes
   and the answer says so, rather than failing loudly or applying twice.
6. **Given** a customer, **When** they try to move any order, including their own, **Then** it is
   refused.

---

### User Story 5 - One customer's addresses are nobody else's (Priority: P1)

No customer can read, change, delete, or check out with another customer's address.

**Why this priority**: An address is where someone lives. A leak is not a bug report; it is a
safety problem.

**Independent Test**: With two real signed-in customers, have the second try every operation on
the first's address, and try checking out with it.

**Acceptance Scenarios**:

1. **Given** customer A's address, **When** customer B reads, edits or deletes it, **Then** B is told
   it does not exist — the same answer as for an address that never existed.
2. **Given** customer A's address, **When** customer B checks out naming it, **Then** checkout is
   refused, without revealing that the address exists.
3. **Given** any request, **When** it names a user in its body or path, **Then** that name is not
   used; the customer is always the one signed in.

---

### Edge Cases

- **The account service does not answer during checkout.** Checkout is refused as temporarily
  unavailable — never placed without an address, and never with a guessed one.
- **The address is deleted between choosing it and checking out.** Checkout is refused as if it
  never existed; the cart is untouched.
- **Two requests set different defaults at the same moment.** The customer ends with exactly one
  default.
- **A failed order** (stock unavailable, payment declined) never enters fulfilment; staff cannot
  move it.
- **Orders placed before this feature** have no address and no delivery option. They are still
  readable, show that no destination was recorded, and are not invented one.
- **Orders that said *Completed* before this feature** read *Paid* afterwards — it is what the word
  meant.
- **A very large number of addresses** — a customer is limited to a reasonable number (see
  Assumptions), so the list cannot be used to store arbitrary data.

## Requirements *(mandatory)*

### Functional Requirements

**Address book**

- **FR-001**: A signed-in customer MUST be able to save, list, edit and delete their own delivery
  addresses.
- **FR-002**: A customer with at least one address MUST have exactly one default; the first address
  saved becomes it, and deleting the default promotes another.
- **FR-003**: An address MUST have a recipient name, a first address line, a city, a postal code and
  a country; a second address line, a region and a phone number are optional.
- **FR-004**: Validation MUST check only that required parts are present and well-formed (lengths,
  a recognised country code, a postal code of plausible shape). It MUST NOT claim that an address is
  deliverable, and the system MUST NOT describe it as "verified".
- **FR-005**: Every address operation MUST act only on the signed-in customer's own addresses; the
  customer MUST be identified from their sign-in, never from the request.
- **FR-006**: Another customer's address MUST be indistinguishable from one that does not exist, to
  every operation.

**Checkout**

- **FR-007**: Checkout MUST accept an address choice and a delivery-option choice; with no address
  choice it MUST use the customer's default, and with no default it MUST be refused.
- **FR-008**: The order MUST record a **copy** of the address as it was at checkout, and the
  delivery option's name and price as they were at checkout. Later changes to either MUST NOT alter
  the order.
- **FR-009**: At least two delivery options with different prices MUST exist, and customers MUST be
  able to see them with their prices before checking out.
- **FR-010**: The amount charged for an order MUST be the items plus the chosen delivery price.
- **FR-011**: If the address cannot be obtained because a service did not answer, checkout MUST be
  refused as temporarily unavailable, and the cart MUST be unchanged.
- **FR-012**: The customer MUST NOT be able to set the delivery price; it comes from the delivery
  option, exactly as item prices come from the catalogue.

**Fulfilment**

- **FR-013**: After a successful checkout the order MUST read *Paid*.
- **FR-014**: Staff MUST be able to move a paid order to *Preparing*, and a preparing order to
  *Shipped* with a tracking reference.
- **FR-015**: No other transition MUST be possible — not backwards, not skipping a step, not from a
  failed order — and a refused transition MUST leave the order unchanged.
- **FR-016**: Repeating a transition that has already happened MUST change nothing and MUST NOT be
  reported as a failure of the system.
- **FR-017**: Only staff MUST be able to move an order through fulfilment.
- **FR-018**: The customer MUST be able to see their order's current state, and the tracking
  reference once shipped.

**Consistency with what exists**

- **FR-019**: Orders placed before this feature MUST remain readable, showing no destination rather
  than an invented one; orders that read *Completed* MUST read *Paid*.
- **FR-020**: Records that previously documented *Paid* as unreachable by design MUST be corrected in
  the same change, and every automated check that expects *Completed* MUST be updated to the new
  meaning rather than removed.

### Key Entities

- **Delivery address** — belongs to exactly one customer: recipient name, address lines, city,
  region, postal code, country, optional phone; whether it is the default.
- **Delivery option** — a named way of sending an order with a price: at least *Standard* and
  *Express*.
- **Order destination** — the copy of an address held by an order: the same fields as a delivery
  address, frozen at checkout, with no link back to the address book.
- **Order delivery** — the chosen option's name and price as frozen at checkout, and, once shipped,
  the tracking reference.
- **Order status** — gains *Paid* (replacing *Completed* as the meaning of a successful checkout),
  *Preparing* and *Shipped*.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A customer can save an address and check out to it using express delivery, and the
  amount charged equals the items plus the express price, in 100% of runs of the end-to-end check.
- **SC-002**: After an order is placed, editing or deleting its address changes the order's recorded
  destination in 0 cases.
- **SC-003**: With two real signed-in customers, every attempt by one to read, change, delete or
  check out with the other's address is refused — 0 successes — and the refusals are identical to
  those for an address that does not exist.
- **SC-004**: Every fulfilment transition that is not the next step is refused, and the order is
  unchanged afterwards, in 100% of attempts.
- **SC-005**: A customer sees their order move Paid → Preparing → Shipped, with the tracking
  reference, without any step being skipped.
- **SC-006**: With the account service unavailable, 0 orders are placed and every attempt is told the
  service is temporarily unavailable.
- **SC-007**: Every existing end-to-end check still passes, updated to say *Paid* where it said
  *Completed*.

## Assumptions

- **Delivery options are a small fixed set**, configured rather than managed through screens. Two is
  enough to make the choice real; a price table by weight or destination is later work.
- **Delivery price does not depend on the destination yet.** Tax and destination-based pricing are
  #21, which depends on this feature.
- **One currency**, as today.
- **A customer may keep up to 20 addresses.** Enough for any real use; small enough that the list
  cannot be used as storage.
- **Postal code validation is shape-only** and permissive: formats differ by country, and rejecting
  a real address is worse than accepting an odd one.
- **Countries are two-letter ISO codes.** Showing country names is a display concern.
- **Staff are the existing Admin role.** A separate warehouse role is later work.
- **An order's address cannot be changed after checkout.** Not now, and not by staff — written down
  so that nobody adds an endpoint for it helpfully. A wrong address is handled outside the system.
- **Cancellation, returns and delivery confirmation** are out of scope; *Shipped* is the last state.
- **Guest checkout** remains out of scope, as in feature 010.

## Out of Scope

- A simulated or real courier, and despatch as an automatic checkout step.
- Tax, and delivery prices that depend on destination or weight (#21).
- Address verification against a postal database.
- Cancelling or editing an order after checkout.
- A separate warehouse role.
