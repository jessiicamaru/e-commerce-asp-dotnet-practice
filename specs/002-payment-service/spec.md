# Feature Specification: Payment Service

**Feature Branch**: `002-payment-service`

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "Payment microservice for the checkout saga. It consumes ProcessPaymentCommand from RabbitMQ via MassTransit and always succeeds (a stub gateway, no real provider integration), replying with PaymentProcessedEvent so the OrderStateMachine saga can reach OrderCompleted and Inventory can confirm its reservation. It records each payment attempt in its own PostgreSQL database so an operator can see what was charged for which order. Consumers must be idempotent because the broker redelivers. Contracts already exist in Ecommerce.Contracts/Payment (ProcessPaymentCommand, PaymentProcessedEvent, PaymentFailedEvent)."

## Context

An order now reaches the point of being paid and stops there. Stock is set aside, the shopper is
waiting, and nothing takes the money — so the hold eventually expires and the units go back on the
shelf as though the order had failed. This is the last missing step in checkout.

> **This service deliberately does not take money.** It approves every payment it is asked to
> process, so the checkout flow can be completed and exercised before a real provider is chosen. It
> is a stand-in, and the spec treats "no money moves" as a property to be made obvious rather than
> a detail to be glossed over — an unnoticed stub in production would fulfil every order for free.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper's order completes (Priority: P1)

A shopper has placed an order and stock is being held for them. Payment is taken, the order is
confirmed, and the units are deducted for good. The shopper's checkout is finished.

**Why this priority**: It is the only path that closes checkout. Everything built so far stops one
step short of it; without this the whole flow still ends in an expired reservation.

**Independent Test**: Place an order for an in-stock product and observe that it reaches a completed
state, and that the held units leave stock permanently rather than returning to it.

**Acceptance Scenarios**:

1. **Given** an order with stock held for it, **When** payment is requested, **Then** the payment is
   approved and the order reaches a completed state.
2. **Given** an order that has just completed, **When** stock is inspected, **Then** the units that
   were held are gone from stock rather than back on the shelf.
3. **Given** a completed order, **When** the shopper's order is inspected, **Then** it shows as paid
   with the amount that was charged.

---

### User Story 2 - An operator can see what was charged (Priority: P2)

Someone answering a customer query, or reconciling at the end of the day, can look up what was
charged against an order and when.

**Why this priority**: A payment nobody can account for is not much of a payment. But it only has
anything to show once the happy path works, so it follows US1.

**Independent Test**: Complete an order, then look up its payment and confirm the amount, the
outcome and the timestamp are all visible without database access.

**Acceptance Scenarios**:

1. **Given** an order that has been paid, **When** its payment is looked up, **Then** the amount,
   the outcome and the time it was processed are returned.
2. **Given** an order that has never been paid, **When** its payment is looked up, **Then** the
   answer says so plainly rather than failing.
3. **Given** any payment record, **When** it is inspected, **Then** it is unmistakable that no real
   money moved.

---

### User Story 3 - A shopper is never charged twice for one order (Priority: P3)

The messaging system can ask for the same payment more than once. The shopper is charged once and
the order completes once.

**Why this priority**: With a stub that charges nothing the harm today is a duplicate record and a
double confirmation downstream. The moment a real provider is connected the same defect becomes a
double charge, so the guarantee is built in now rather than retrofitted.

**Independent Test**: Request the same payment several times and confirm exactly one payment record
exists and stock is deducted only once.

**Acceptance Scenarios**:

1. **Given** a payment already processed for an order, **When** the same request arrives again,
   **Then** no second payment is recorded and the original outcome stands.
2. **Given** a repeated request, **When** it is processed, **Then** the order is not confirmed a
   second time and stock is not deducted twice.

---

### Edge Cases

- **Repeated delivery.** The same payment request may arrive more than once; it must be paid once.
- **Zero or negative amount.** An order asking to be charged nothing, or a negative sum, is not a
  valid payment and must not be silently approved.
- **Payment for an unknown order.** The service is told to charge an order it has never heard of.
  It has no way to verify orders, so it treats the request as authoritative — but this must be a
  conscious decision, not an oversight.
- **Two requests for one order arriving at once.** Exactly one payment is recorded, never two.
- **A reply that never arrives.** If the service records a payment but its reply is lost, the order
  is left waiting and its stock eventually expires. The payment record and the order then disagree,
  which an operator must be able to see.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST respond to every payment request with either an approval or a
  rejection. No request may go unanswered.
- **FR-002**: The system MUST approve every valid payment request, without contacting any external
  provider and without moving any money.
- **FR-003**: The system MUST record every payment attempt, including which order it was for, the
  amount, who it was for, the outcome, and when it was processed.
- **FR-004**: The system MUST reject a request for a non-positive amount, and the rejection MUST
  carry a human-readable reason.
- **FR-005**: Processing the same payment request more than once MUST record one payment and
  produce one outcome.
- **FR-006**: Concurrent requests for the same order MUST result in exactly one payment record.
- **FR-007**: The system MUST expose a way to look up the payment for an order, so a payment can be
  accounted for without direct database access.
- **FR-008**: Payment records MUST be visibly marked as produced by a stand-in rather than a real
  provider, so that no record can be mistaken for evidence that money moved.
- **FR-009**: The system MUST own its payment records independently of any other part of the
  platform.
- **FR-010**: A rejection MUST NOT leave the order's stock held; the existing compensation path must
  be able to run.
- **FR-011**: The outcome the system produces MUST be selectable by configuration, so the rejection
  path and the compensation that follows it can be exercised deliberately. The setting applies to
  the whole service and defaults to approving.
- **FR-012**: The configured outcome MUST be reported at startup and through the service's health
  information, so that a service set to reject is never mistaken for one that is broken, and a
  service set to approve is never mistaken for one that is really charging.

### Key Entities

- **Payment**: One attempt to charge for one order. Records the order, the payer, the amount, the
  outcome, the reason when rejected, and when it was processed. At most one per order.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of orders with stock available reach a completed state, rather than stalling
  after stock is held. Today this figure is 0%.
- **SC-002**: A shopper's order completes within 5 seconds of payment being requested, for 99% of
  orders under normal load.
- **SC-003**: Every completed order has exactly one payment record, verifiable by counting records
  against completed orders.
- **SC-004**: Repeating a payment request produces no additional payment record and no further
  change to stock, verified by processing the same request twice.
- **SC-005**: Across 50 simultaneous requests for one order, exactly one payment is recorded.
- **SC-006**: An operator can establish what was charged against any order, and that no money moved,
  without being given database access.
- **SC-007**: Stock held for a paid order is permanently deducted rather than returned, verifiable
  by comparing quantity on hand before and after.
- **SC-008**: With the service set to reject, an order fails and its held stock is available again
  within 5 seconds — the compensation path runs end to end without anyone publishing a message by
  hand.

## Assumptions

- The checkout process already requests payment at the right moment; this feature supplies the
  answer rather than changing when payment happens.
- The message contracts for requesting payment and for reporting its outcome already exist and are
  not being redesigned.
- Refunds, partial payments, instalments, currency conversion, payment methods, saved cards and
  chargebacks are all out of scope. A payment is a single whole amount, approved or rejected.
- The amount to charge is supplied with the request and is trusted. This service does not
  recalculate it or check it against the order.
- The service cannot verify that an order exists; it acts on the request it is given.
- No real provider is integrated, and no credential, card number or personal payment detail is ever
  handled or stored. That is what makes this safe to build as a stand-in.
- Replacing the stand-in with a real provider will change how an outcome is decided, but not the
  messages exchanged or the records kept.
- The outcome setting is a development and testing affordance. It defaults to approving, and a
  deployment that ever ran this service for real would be wrong regardless of how it is set.
