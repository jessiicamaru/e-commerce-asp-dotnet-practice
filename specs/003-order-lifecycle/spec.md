# Feature Specification: Order Lifecycle Visibility

**Feature Branch**: `003-order-lifecycle`

**Created**: 2026-09-16

**Status**: Draft

**Input**: Issue [#2](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/2) — "orders never leave Submitted, and cannot be read back"

## Why this exists

The checkout saga now runs end to end: stock is reserved, payment is recorded, stock is permanently
deducted. The one participant that never finds out is the order itself. Every order row in the
database reads `Submitted`, including the eleven that completed successfully, and there is no
endpoint that would show a shopper otherwise.

That makes the order record actively misleading rather than merely incomplete: a shopper whose
payment was rejected and whose stock was returned to the shelf has an order that looks exactly like
one still being processed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A finished order says it finished (Priority: P1)

A shopper submits an order. Stock is reserved, the payment succeeds, the goods are committed. The
order record reflects that it is done.

**Why this priority**: This is the defect in the issue. Until the order row settles, every other
part of this feature reports something untrue — a read endpoint built first would simply show
`Submitted` more conveniently.

**Independent Test**: Submit an order through the running system, let the saga finish, and read the
order's status back from the database. No other part of this feature is required.

**Acceptance Scenarios**:

1. **Given** an order that has been submitted and whose checkout has completed, **When** the order
   record is read, **Then** it shows the order as completed and records when that happened.
2. **Given** an order already recorded as completed, **When** the same completion notice arrives
   again, **Then** the record is unchanged — same status, same timestamp.
3. **Given** an order already recorded as completed, **When** a contradicting failure notice
   arrives for it, **Then** the record stays completed.

---

### User Story 2 - A failed order says why it failed (Priority: P2)

A shopper's order cannot be fulfilled — either the stock could not be reserved, or the payment was
rejected. The order record says so, and says which.

**Why this priority**: A failed order that is indistinguishable from an in-flight one is the most
expensive kind of wrong data: nobody knows whether to wait or to act. It is second only because P1
establishes the mechanism this story reuses.

**Independent Test**: Configure the payment stand-in to reject, submit an order, and read the order
back. Its reason should name the rejection. Separately, submit an order for more units than exist
and confirm the reservation failure is recorded the same way.

**Acceptance Scenarios**:

1. **Given** an order whose payment was rejected, **When** the order record is read, **Then** it
   shows the order as failed and carries a non-empty reason describing the rejection.
2. **Given** an order whose stock could not be reserved, **When** the order record is read, **Then**
   it shows the order as failed with the reservation's reason.
3. **Given** an order already recorded as failed, **When** the same failure notice arrives again,
   **Then** the record is unchanged, including its original reason.
4. **Given** a failure notice for an order this service has never seen, **When** it is delivered,
   **Then** it is discarded without repeated redelivery and without failing the system.

---

### User Story 3 - A shopper can look at their own orders (Priority: P3)

A shopper lists the orders they have placed, newest first, and opens one to see what was in it and
how it ended.

**Why this priority**: The status is only useful once somebody can see it. It is third because it
delivers nothing new until P1 and P2 make the status true — but without it, the outcome of this
whole feature is visible only to someone with database access.

**Independent Test**: Sign in as two different shoppers, place an order as each, and confirm each
sees exactly their own — both in the list and when asking for the other's order by its identifier.

**Acceptance Scenarios**:

1. **Given** a signed-in shopper with several orders, **When** they list their orders, **Then** they
   see all of their own, newest first, and none belonging to anyone else.
2. **Given** a signed-in shopper, **When** they open one of their own orders, **Then** they see its
   status, its total, when it was placed and last changed, its line items, and its failure reason if
   it has one.
3. **Given** a signed-in shopper, **When** they ask for an order identifier belonging to another
   shopper, **Then** the response is indistinguishable from asking for one that does not exist.
4. **Given** a caller with no valid credentials, **When** they list or open any order, **Then** the
   request is rejected before any order data is read.
5. **Given** a shopper with more orders than fit on one page, **When** they ask for a later page,
   **Then** they receive that page along with enough information to know how many orders there are
   in total.

---

### Edge Cases

- **The same notice arrives twice.** The broker redelivers on its own schedule; a second delivery
  must leave a settled order exactly as the first left it, reason and timestamp included.
- **Two contradicting notices arrive.** A completion and a failure for one order should never both
  be sent, but a guarded transition must survive it rather than letting whichever arrives last win.
- **A notice arrives for an order that does not exist here.** For example after a database reset, or
  a message left over from another environment. It must not be retried forever and must not take the
  service down.
- **An order has no line items.** It cannot be created that way today, but a detail view must not
  fail on one.
- **A page beyond the last one is requested.** An empty page is a correct answer, not an error.
- **An order that is still in flight is read.** It legitimately shows as submitted; nothing about
  this feature makes an in-progress order an error case.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST record an order as completed when its checkout completes, together
  with the time of completion.
- **FR-002**: The system MUST record an order as failed when its checkout fails, together with the
  reason the checkout reported and the time of failure.
- **FR-003**: Both failure paths — stock could not be reserved, and payment was rejected — MUST
  produce a recorded failure on the order.
- **FR-004**: Repeating a completion or failure notice MUST NOT change an order that has already
  settled, in any field.
- **FR-005**: A notice MUST NOT move an order out of a settled state, even when it contradicts the
  state already recorded.
- **FR-006**: A notice naming an order this service does not hold MUST be discarded without error
  and without indefinite redelivery, and the fact MUST be recorded where an operator can see it.
- **FR-007**: Shoppers MUST be able to retrieve the orders they have placed, ordered newest first,
  in pages, with the total count available to them.
- **FR-008**: Shoppers MUST be able to retrieve one of their own orders including its line items,
  its status, its totals, its timestamps and its failure reason when it has one.
- **FR-009**: The shopper whose orders are returned MUST be determined from the caller's
  credentials, never from an identifier supplied in the request.
- **FR-010**: A request for an order that exists but belongs to another shopper MUST be answered
  identically to a request for an order that does not exist.
- **FR-011**: Requests without valid credentials MUST be rejected before any order is read.
- **FR-012**: Order statuses that no part of the system can currently produce MUST be documented as
  unreachable, so that nobody reads the list of statuses as a list of things that can happen.

### Key Entities

- **Order**: What a shopper asked to buy. Carries who placed it, the total, its current status, the
  reason it failed when it did, when it was created and when it last changed, and its line items.
  The order is the owner of its own status — no other service records it.
- **Order line item**: One product on an order, with the quantity and the price at the time of
  ordering.
- **Order status**: Where the order has got to. Four values are reachable after this feature:
  awaiting submission, submitted, completed, failed. Three further values exist in the data model
  and are **not** reachable — see Assumptions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An order whose checkout completes shows as completed within 5 seconds of the checkout
  finishing, at the 99th percentile.
- **SC-002**: An order whose payment is rejected shows as failed with a non-empty reason, and the
  stock it held is available to other shoppers again.
- **SC-003**: Delivering the same settlement notice 10 times produces exactly one settled order
  whose fields are identical after the tenth delivery and after the first.
- **SC-004**: Across two shopper accounts, each sees 100% of their own orders and 0% of the other's,
  both when listing and when asking by identifier.
- **SC-005**: A shopper can determine the outcome of any order they placed without database access,
  in a single request.
- **SC-006**: After a run of orders through the system, 0 orders that have resolved remain in a
  non-final state one minute later.

## Assumptions

- **Intermediate statuses stay unreachable.** `StockReserved` and `Paid` exist in the data model but
  nothing publishes an event that could set them. The user decided on 2026-09-16 to build the
  settled states only: doing otherwise would mean adding a record to the shared message contracts,
  which every service deserializes, for a progress indicator no screen currently shows. The values
  are kept rather than deleted, because existing rows and the saga's vocabulary both use them, and
  FR-012 requires the gap to be written down rather than left to be rediscovered.
- **`Cancelled` is out of scope.** It belongs to a shopper-initiated cancellation that does not
  exist yet; nothing in this feature can produce it.
- **No new cross-service message is needed.** The checkout orchestrator already announces both
  completion and failure, and both announcements already carry everything these requirements need.
  This feature adds listeners, not contracts.
- **No administrative cross-shopper view.** Reading somebody else's orders is not part of this
  feature; the only reader is the shopper who placed them.
- **Paging defaults**: 20 orders per page, first page by default, capped so a single request cannot
  ask for an unbounded number.
- **The reason text comes from whoever failed the checkout.** This feature records it verbatim
  rather than translating it into a shopper-facing message; wording is a separate concern.

## Dependencies

- The checkout orchestrator already publishes a completion announcement and a failure announcement,
  on both failure paths. Verified in the orchestrator's state machine, not assumed.
- The Order service already writes its outgoing messages and its data in one transaction. This
  feature needs the same guarantee applied to incoming messages, which is not yet configured there.
