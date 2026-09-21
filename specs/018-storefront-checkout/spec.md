# Feature Specification: Checkout and Order History

**Feature Branch**: `018-storefront-checkout` · **Created**: 2026-09-22 · **Status**: Implemented

**Input**: Issues [#38](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/38) and
[#39](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/39), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

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
