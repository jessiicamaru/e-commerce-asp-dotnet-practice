# Feature Specification: Checkout and Order History

> Completed on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature Branch**: `018-storefront-checkout` · **Created**: 2026-09-22 · **Status**: Implemented

**Merged**: [#48](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/48), 2026-09-22 (06:01, UTC+7)

**Input**: Issues [#38](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/38) and
[#39](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/39), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

The last pages of #23: a person can find a product, keep it in a cart and name an address, but cannot
buy it without a terminal, and cannot see an order afterwards. Checkout is also where the storefront
meets the saga - an order is `Submitted` at once and settles two or three seconds later - and #23 asked
how that should be presented ("Poll, or push?").

## User Scenarios & Testing

### User Story 1 - A customer sees exactly what they will pay, then pays it (Priority: P1)

**Why this priority**: it is the checkout. A total shown and a different total charged would be the
worst failure a shop can have (specs/012).

**Independent Test**: with a cart and an address, open `/checkout`, note the total, place the order;
the order's stored total equals the one shown.

**Acceptance Scenarios**:

1. **Given** a cart and at least one address, **When** checkout opens, **Then** the default address
   (or the first) and the first delivery option are preselected, and the lines, subtotal, delivery,
   tax with its rate, and total are shown **before** anything is placed.
2. **Given** a different address or option, **When** chosen, **Then** the breakdown is priced again by
   the server.
3. **Given** the breakdown shown, **When** the order is placed, **Then** the order's stored parts are
   identical to it, unless the cart or a price changed in between.
4. **Given** an empty cart or no address, **When** checkout prices it, **Then** the server's refusal
   (409) is shown in words; an unknown delivery option is a 400; a service needed for pricing being down
   (503) says so.

### User Story 2 - A customer waits for the saga, and is told honestly what is happening (Priority: P1)

**Why this priority**: equal first; #38 is titled "wait for the saga honestly".

**Independent Test**: place an order; the order page says the items are being reserved and paid for,
then shows `Paid` within seconds.

**Acceptance Scenarios**:

1. **Given** an order just placed, **When** the order page opens, **Then** it polls every second while
   the order is `Submitted`, with a sentence saying stock is being reserved and payment taken.
2. **Given** a paid order, **When** it settles, **Then** the page says "Thank you for your order" and
   shows the lines, stored totals and destination.
3. **Given** the saga has not settled after 30 polls (about 30 seconds), **When** the limit is reached,
   **Then** the page stops polling and says so.
4. **Given** a declined payment, **When** the order fails, **Then** the page reads "the payment was
   declined. Your cart is unchanged", links back to the cart, and the cart still holds the items.
5. **Given** a stock failure, **When** the order fails, **Then** the page says some items ran out of
   stock and nothing was charged.

### User Story 3 - A customer reads their orders (Priority: P2)

**Why this priority**: second - after the purchase, not part of it.

**Independent Test**: `/orders` lists the customer's orders newest first; opening one shows its lines,
totals, destination, state and tracking reference.

**Acceptance Scenarios**:

1. **Given** several orders, **When** `/orders` opens, **Then** they are listed newest first, 10 to a
   page, each with its total and a status sentence.
2. **Given** a shipped order, **When** opened, **Then** it shows its tracking reference.
3. **Given** another customer's order id, **When** opened, **Then** it reads "Order not found." - the
   same as an id that does not exist.

### Edge Cases

- **The cart or a price changes between quote and order**: the order is priced again by the same code,
  so it charges the new figure; the quote is not a reservation.
- **Failure reasons are written for operators** (`Insufficient stock for product 01a0...`): the client
  classifies them into sentences and never shows them raw.
- **An order from before specs/012** has no stored parts; the page shows what there is.
- **Leaving the order page while it polls** stops the polling.

## Requirements

- **FR-001**: At checkout the customer chooses an address (default preselected) and a delivery option.
- **FR-002**: Before placing the order, the page shows the lines, subtotal, delivery, tax (with the
  rate) and total. **These numbers come from Order**, not from the client.
- **FR-003**: The total shown is the total charged, unless the cart or a price changes in between.
- **FR-004**: After placing, the order page polls until the saga settles it, and says what is
  happening meanwhile. If the saga has not settled it after 30s, the page stops polling and says so.
- **FR-005**: A declined payment reads as a declined payment, and the cart is still there.
- **FR-006**: The customer's orders, newest first, with a status sentence. One order shows its
  lines, totals, destination, fulfilment state and tracking reference.
- **FR-007**: Another customer's order is simply not found.
- **FR-008**: The quote MUST be computed by the same code as the order, and MUST place, stage and
  publish nothing.
- **FR-009**: The quote MUST refuse exactly what checkout refuses: empty cart or no address 409, an
  address that is not the caller's 404, an unknown delivery option 400.

### Key Entities

- **Checkout quote** - the lines with their tax, the address, the delivery option and its price,
  subtotal, tax total, discount, tax rate and total; computed, never stored.
- **Order (as shown)** - status, failure reason, lines, the stored parts, destination, delivery option,
  tracking reference.
- **Order summary** - id, total, status, failure reason, item count, dates.

## Success Criteria

- **SC-001**: For the same cart and choices, the quote's parts equal the placed order's stored parts
  exactly (`CheckoutQuoteTests`; Bruno's `checkout` asserts it charged the quoted total).
- **SC-002**: A settled order is shown settled within one polling interval of settling.
- **SC-003**: After a declined payment the cart holds every unit it held before, in 100% of runs.
- **SC-004**: Another customer's order is a 404, indistinguishable from a missing one.

## What building it found

**The backend could not show a total before the order existed.** Tax depends on the destination and
is rounded per line (ADR-002). Without a quote endpoint the client would have had to copy those rules,
and a copy that drifts means a customer agrees to one number and is charged another. Per #23's rule
(what the interface needs, the backend provides), this feature adds `GET /api/orders/quote`. The
pricing moved out of `SubmitOrderCommandHandler` into `CheckoutPricing`, which both call, so the two
cannot disagree by construction. `CheckoutQuoteTests` proves it, and a mutation that skews the quote by
0.01 makes that test fail.

**Failure reasons are written for operators** (`Insufficient stock for product 01a0...`,
`Payment declined by the stub gateway (PAYMENT_OUTCOME=Reject)`). The client classifies them into
sentences rather than showing them raw. Not filed as an issue: a stored reason for operators is
legitimate. A customer-facing reason *code* would be the better contract if this storefront became
real.

#38 and #39 ship together because the order page is shared: checkout lands on it, and history links to it.

## Assumptions

- The saga settles an order in about two seconds (the order page's own comment); 30 polls leaves a wide
  margin.
- Payment is the stub, so both outcomes are reachable by restarting it with `PAYMENT_OUTCOME`.
- Delivery options are Order's configured `standard` and `express` (specs/011).

## Out of Scope

- Push (WebSocket or server-sent events) instead of polling - "push can come later" (#34).
- A customer-facing failure reason code (recorded above, not filed).
- Cancelling an order (specs/039).
