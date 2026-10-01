# Feature Specification: A signed-out shopper is offered Add to cart

**Feature Branch**: `feat/252-signed-out-add-to-cart` | **Created**: 2026-10-01 | **Issue**: #252

**Status**: Draft

**Input**: Issue #252 - "a signed-out shopper is offered Add to cart" - found in the screen review of 2026-10-01.

## Why

Somebody who has not signed in - most first visits - saw no Add to cart on a product page, only a line of text with a link. A shop that hides its buy button until you have an account loses the people deciding whether to make one. The page also printed the first variant's SKU while nothing was chosen, and opening the cart signed out showed the sign-in form with no word of why.

## User Scenarios & Testing *(mandatory)*

### US1 - The buy button is there for everybody (Priority: P2)



**Acceptance Scenarios**:

1. **Given** a signed-out shopper on a product page, **Then** the same quantity and Add to cart are shown as for a customer, with the same rules (choose a variant first; none left disables it).
2. **When** they press Add to cart, **Then** the sign-in page opens saying they need to sign in to add it to their cart, and after signing in they are back on the product **with the variant they chose still chosen**.
3. **Given** the shopper opens `/cart` signed out, **Then** the sign-in page says it is to see their cart.

---

### US2 - The page names a variant only once one is chosen (Priority: P3)



**Acceptance Scenarios**:

1. **Given** a product with several variants and none chosen, **Then** no SKU is shown; once one is chosen, its SKU is.

### Edge Cases

- **Not preselecting a variant - a deliberate decision kept** (specs/020 research D10): with several shapes the shopper chooses; defaulting sells a kit nobody chose. The issue suggested preselecting the first in stock; research D1 here says why that stays as it was. A product sold one way is chosen already, as before.
- The cart itself stays per account (Cart service): nothing is added before sign-in, so nothing is lost or merged. Adding after returning is one more press, on purpose - the quantity and price may have been seen minutes earlier.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Signed out, `AddToCart` shows the quantity and the button; pressing it goes to `/sign-in` with `state.from` = the product address including `?variant=<id>` when one is chosen, and a reason.
- **FR-002**: The product page initialises the chosen variant from `?variant=` when it names a sellable one.
- **FR-003**: The sign-in page says why when it was reached to add to the cart, or from `/cart`.
- **FR-004**: The product page shows a SKU only when a variant is chosen (or the product is sold one way).

## Success Criteria *(mandatory)*

- **SC-001**: A signed-out shopper reaches sign-in from Add to cart and comes back with their choice (tested).
- **SC-002**: No SKU is shown for an unchosen variant (tested).

## Assumptions

- The sign-in page already returns to `state.from` (specs/016); the address carries the choice.
