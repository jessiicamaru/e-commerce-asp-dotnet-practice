# Feature Specification: Pay with VNPay (sandbox)

**Feature Branch**: `feat/289-vnpay-sandbox`
**Created**: 2026-10-03
**Status**: Draft
**Issue**: #289
**Input**: "Payment approves every order without moving money. A shop selling in dong would take payment through a
Vietnamese gateway: the customer pays on the gateway's page and the gateway tells the shop by a signed server-to-server
call (IPN)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A customer pays on VNPay's page (Priority: P1)

A customer places an order in dong. Their order page offers **Pay with VNPay**. They pay on the gateway's page and are
sent back to the shop. The order turns **Paid** once the gateway has told the shop itself; what the browser's address
says is never trusted.

**Why this priority**: it is the feature. Without it the shop takes no payment at all.

**Independent Test**: with Payment set to VNPay and the simulator running:
1. Place an order and open the pay link.
2. Pay on the simulator, which calls the IPN and redirects back.
3. The order is Paid, the stock is deducted, and the payment row says `VnPaySandbox` with the gateway's transaction
   number.

**Acceptance Scenarios**:

1. **Given** an order waiting for payment, **When** its owner opens it, **Then** the page offers a signed link to the
   gateway, valid for the payment window.
2. **Given** the gateway reports success through a correctly signed IPN with the right amount, **When** it arrives,
   **Then**:
   - one payment is recorded as approved;
   - the saga is told, and the order settles to Paid;
   - the gateway's answer is `00`.
3. **Given** the customer returns to the shop, **When** the return page opens, **Then** it shows the order as the shop
   records it, polling until it settles. It never shows the outcome carried in the query string.

---

### User Story 2 - A payment that does not happen fails the order (Priority: P1)

The customer cancels on the gateway's page, or the gateway declines the payment. The order fails and its stock goes back
on the shelf, the same as a declined payment today.

**Why this priority**: a payment that never arrives must not hold stock for ever.

**Independent Test**: cancel on the simulator. The order is Failed, the reservation is released, and nothing is held.

**Acceptance Scenarios**:

1. **Given** an IPN with a failure code (for example `24`, cancelled by the customer), **When** it arrives, **Then** one
   rejected payment is recorded with the reason, and the order fails and releases its stock.
2. **Given** the customer never pays, **When** the payment timeout passes (specs/053), **Then** the saga fails the
   order. A late approval after that is refunded, as today.

---

### User Story 3 - Forged and repeated notifications change nothing (Priority: P1)

The IPN address is public. It must accept only what the gateway signed, for the amount the shop asked for, once.

**Why this priority**: a forged IPN is a free order.

**Independent Test**: send a signed IPN twice, an IPN with a wrong signature, one with the wrong amount, and one for an
unknown reference. Exactly one payment and one event exist, and each answer carries the gateway's code (`02`, `97`,
`04`, `01`).

**Acceptance Scenarios**:

1. **Given** a wrong or missing signature, **When** it arrives, **Then** the answer is `97` and nothing is written.
2. **Given** an amount different from the order's, **Then** `04`, and nothing is written.
3. **Given** an unknown reference, **Then** `01`.
4. **Given** a reference already confirmed, **Then** `02`, with no second payment and no second event.
5. **Given** two copies of the same IPN at once, **Then** one payment and one event.

---

### User Story 4 - Development, CI and Bruno run the whole flow without VNPay (Priority: P2)

A simulator speaks VNPay's protocol with the same signing: a pay page with **Pay** and **Cancel**, the IPN call, and
the redirect back. Development and CI use it; the real sandbox needs only a different address and the merchant's
codes.

**Independent Test**: CI's saga job runs a VNPay scenario against the simulator: one order paid, one cancelled.

### Edge Cases

- **An order not in dong**: VNPay settles in VND only. The order is refused with a reason that says so, and fails like
  a declined payment. The stub still serves every currency when it is the provider.
- **Payment's message arrives before the customer looks**: the page polls. Until Payment has the checkout it says the
  payment is being prepared.
- **Someone else's order**: 404, the same as a missing one.
- **The IPN arrives before the return**: normal. The return page reads the record either way.
- **The payment window**: the link expires (`vnp_ExpireDate`) before the saga's timeout. Configuration says so, and
  the startup check refuses a window that is not shorter.
- **Refunds**: still recorded, not sent to VNPay (decided: see research D7).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The provider is configuration: `PAYMENT_PROVIDER=Stub` (the default) or `VnPay`. CI and development keep
  the stub unless asked.
- **FR-002**: With VNPay, a payment request opens a **checkout** (one per order) and decides nothing. The saga waits.
- **FR-003**: The order's owner reads the checkout: state, expiry and a freshly signed pay URL (HMAC-SHA512, VNPay 2.1.0).
- **FR-004**: The IPN endpoint:
  - is anonymous;
  - verifies the signature, the merchant code and the amount;
  - records exactly one payment per order and publishes its event in the same transaction;
  - answers VNPay's codes.
- **FR-005**: The stub's three signals hold for the sandbox too: the provider is on every row, a warning is logged at
  startup, and `/health` says so (`movesMoney: false`).
- **FR-006**: The storefront offers the pay link while the order waits, and a return page that shows the shop's
  record.
- **FR-007**: A simulator implements the pay page, the IPN call and the redirect with the same signing. It runs in
  compose and CI, and never in production.
- **FR-008**: `TmnCode` and `HashSecret` are settings. The simulator's are development values; the sandbox's belong to
  the merchant and are never committed.

### Key Entities

- **Payment checkout**: one per order waiting at a redirect gateway. It holds the order, owner, amount, currency,
  reference, opened and expires times, completion, the gateway's transaction number and its response code.
- **Payment**: unchanged in meaning. It gains the gateway's own reference.

## Success Criteria *(mandatory)*

- **SC-001**: Against the simulator, a paid order settles to Paid, and a cancelled one fails with its stock released.
  CI asserts both.
- **SC-002**: A forged, replayed, wrong-amount or unknown IPN writes nothing. Tests assert each case.
- **SC-003**: With the stub (the default), every existing test and the saga job's two branches behave as before.
- **SC-004**: With sandbox credentials, the same flow works against VNPay's sandbox. This is the owner's step and is
  recorded as not yet done.

## Assumptions

- VNPay's 2.1.0 protocol as published in its merchant documentation:
  - `vnp_*` parameters;
  - HMAC-SHA512 over the URL-encoded, key-sorted query;
  - amount in hundredths of a dong;
  - IPN answered with `{"RspCode","Message"}`.
- One attempt per order. A customer who cancels places the order again, as after a declined card today.
