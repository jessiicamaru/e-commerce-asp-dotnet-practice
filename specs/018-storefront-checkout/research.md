# Research: Checkout and Order History

> Written on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

## D1 - A quote endpoint, priced by the same code as the order

**Decision**: add `GET /api/orders/quote?addressId=&shippingOption=`, and move the pricing out of
`SubmitOrderCommandHandler`, unchanged, into `CheckoutPricing.PriceAsync`, which both the quote and the
order call.

**Rationale**: "Tax depends on the destination and is rounded per line (ADR-002). Without a quote
endpoint the client would have had to copy those rules, and a copy that drifts means a customer agrees
to one number and is charged another." With one class behind both, "the only way they can differ is if
the cart, a price or the address changed in between" (`CheckoutPricing`'s comment). #23's rule applied:
what the interface needs, the backend provides.

**Alternatives considered**:

- **Compute the breakdown in the client.** Rejected: a second copy of ADR-002's rules.
- **A second pricing path in the quote handler.** Rejected: two copies of the same rules on the server
  are the same risk as one on the client. `CheckoutQuoteTests` fails if the two ever disagree.
- **Place the order, then show its parts** (what specs/012 made possible). Rejected: the customer would
  see the total only after committing to it - which #38's acceptance ("see subtotal, delivery, tax and
  total before paying") rules out.

## D2 - The quote refuses exactly what checkout refuses

**Decision**: one FluentValidation rule, `MustBeADeliveryOption`, used by both validators (not empty;
one of the configured codes, and the message lists them), and the rest of the refusals inside
`CheckoutPricing`: empty cart 409, no address and no default 409, an address id not the caller's 404,
unsellable 409.

**Rationale**: a quote that accepted what checkout refuses would show a total nobody could pay.

**Alternatives considered**: not recorded.

## D3 - Poll the order, every second, at most 30 times

**Decision**: the order page reads `GET /api/orders/{id}` every 1000 ms while the status is
`Submitted`, and stops after 30 polls, saying so. It stops on unmount.

**Rationale**: the storefront-wide decision on #34 - "Checkout is presented by polling the order until
the saga settles it; push can come later." The saga settles "in about two seconds" (the page's
comment), so 30 s is a wide margin; stopping is honest about a stall rather than spinning for ever.

**Alternatives considered**: push (#23 asked "Poll, or push?"). Rejected for now by #34; polling is what
`verify-saga.sh` already does.

## D4 - Statuses and failure reasons as sentences, classified in the client

**Decision**: `describeStatus(status, failureReason)` - `Submitted` "We are reserving your items and
taking payment…", `Paid`, `Preparing`, `Shipped`, and for `Failed` a sentence chosen by matching the
reason: `/stock/i` → ran out of stock; `/payment|declin/i` → payment declined; otherwise not placed.
Each failure sentence says nothing was charged and the cart is unchanged.

**Rationale**: "Failure reasons are written for operators ... The client classifies them into sentences
rather than showing them raw." #39: "Statuses read as sentences a customer understands."

**Alternatives considered**: a customer-facing reason **code** from the server. Recorded as "the better
contract if this storefront became real", and not filed: "a stored reason for operators is legitimate".

## D5 - #38 and #39 in one pull request

**Decision**: checkout and order history ship together.

**Rationale**: "the order page is shared: checkout lands on it, and history links to it."

**Alternatives considered**: two pull requests. Not taken for that reason.
