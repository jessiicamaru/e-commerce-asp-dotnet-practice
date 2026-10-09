# Feature Specification: A cart before signing in

**Feature Branch**: `feature/370-guest-cart`
**Created**: 2026-10-09
**Status**: Draft
**Issue**: #370
**Input**: "A shopper who is not signed in adds to, changes and removes from a cart kept in the browser; on sign-in or
registration it is merged into the account's cart; checkout still requires an account."

## Context

The cart is the Cart service's, keyed by the signed-in customer (specs/010). `CartController` is `[Authorize]` and
`/cart` is behind `RequireAuth`, so "Add to cart" sends a signed-out shopper to sign in first (specs/126 made that
journey come back to the chosen variant). The moment is lost before anything is chosen - the most common way a first
visit ends. Found in the review of 2026-10-09.

## User Scenarios & Testing

### User Story 1 - A shopper fills a cart without an account (Priority: P1)

Signed out, a shopper adds products, sees the count in the header, opens the cart, changes quantities and removes
lines; the cart shows names, prices and the estimate exactly as a signed-in cart does, and survives a reload.

**Why this priority**: it is the feature.

**Independent Test**: signed out, add two shapes of one product and one of another; reload; open `/cart`: three lines,
priced, with the estimate; change one, remove one.

**Acceptance Scenarios**:

1. **Given** a signed-out shopper, **When** they add a product, **Then** it is in their cart, the header shows the count,
   and nothing is stored on the server.
2. **Given** that cart, **When** they open `/cart`, **Then** each line is named and priced in the language and currency
   being browsed, with the same statuses as a signed-in cart (not for sale, no longer available, not sold in this
   currency) and the same estimate.
3. **Given** that cart, **When** they reload or come back later on the same browser, **Then** it is still there.
4. **Given** the guest cart, **When** they press checkout, **Then** they are asked to sign in or register and come back to
   the cart.

---

### User Story 2 - Signing in keeps what was chosen (Priority: P1)

On signing in or registering, the browser's cart is merged into the account's cart and emptied; nothing is lost, and
nothing doubles if it happens twice.

**Why this priority**: a guest cart that vanishes at sign-in is worse than none.

**Independent Test**: signed out, add A ×2 and B ×1; sign in to an account whose cart holds A ×1 and C ×1: the cart is
A ×2, B ×1, C ×1, and the browser's cart is empty. Repeat the same merge: unchanged.

**Acceptance Scenarios**:

1. **Given** a guest cart and an account cart, **When** the shopper signs in, **Then** the account cart holds every line
   of both - for a shape in both, **the larger quantity** - and the browser's cart is emptied.
2. **Given** the same merge sent twice (a retry, two tabs), **Then** the account cart is the same as after one.
3. **Given** a guest line the server refuses (a product since deleted), **Then** the rest are merged and the shopper
   loses nothing else.

### Edge Cases

- **Many lines**: a browser cart holds at most 50 lines; pricing and merging refuse more.
- **Bad data in the browser** (edited, from an older version): unreadable lines are dropped when read, never sent.
- **Storage unavailable** (private mode): the shop still works; adding while signed out says the cart cannot be kept
  in this browser and offers sign-in, as before.
- **Signed in**: nothing changes - the account's cart is used, and the browser's is not read except to merge.

## Requirements

### Functional Requirements

- **FR-001**: A signed-out shopper MUST be able to add to, change and remove from a cart kept in the browser (product,
  variant and quantity only - never a price).
- **FR-002**: Catalog-priced reading of such a cart MUST go through the server - an anonymous request that prices given
  lines exactly as a stored cart is priced and **stores nothing**.
- **FR-003**: On signing in (password, two-factor or registration) the browser's cart MUST be merged into the account's
  in one request and one transaction, taking the larger quantity for a shape in both, then emptied.
- **FR-004**: The merge MUST be idempotent: repeating it changes nothing.
- **FR-005**: Checkout MUST still require an account; the guest cart page offers sign-in and returns to the cart.
- **FR-006**: Both new requests MUST refuse more than 50 lines and a quantity under 1 or over 999.
- **FR-007**: The header MUST show the guest cart's count, and `/cart` MUST open signed out.

### Key Entities

- **Guest cart**: lines `{ productId, variantId, quantity }` in the browser's storage - no prices, no owner.

## Success Criteria

- **SC-001**: A signed-out shopper adds, reloads, changes and removes, and the cart reads back priced, in a browser.
- **SC-002**: The anonymous pricing answers the same lines, statuses and estimate as the stored cart for the same lines
  (tested), and writes no row.
- **SC-003**: The merge yields the larger quantity per shape, keeps the other lines, and is unchanged by a second
  identical merge (tested on PostgreSQL).
- **SC-004**: Bruno covers both endpoints and their refusals; `docs/reference` regenerated.

## Assumptions

- "The larger quantity" rather than "the sum" (research D2): it is what makes a repeated merge harmless, and a shopper
  who chose 2 on a phone and 2 in the browser rarely wants 4.
- The browser's cart is per browser; it does not follow the shopper between devices until they sign in.
