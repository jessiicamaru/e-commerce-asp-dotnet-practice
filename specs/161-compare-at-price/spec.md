# Feature Specification: A compare-at price per variant

**Feature Branch**: `feature/369-compare-at-price`
**Created**: 2026-10-09
**Status**: Draft
**Issue**: #369
**Input**: "Per variant and per currency, an optional compare-at (original) price, higher than the selling price, shown
struck through with the percentage off. The price charged does not change. The listing can filter to discounted
products."

## Context

A variant has one price per currency (specs/022). A seller cannot show a reduction - "~~1,200,000₫~~ 990,000₫ -17%" -
except by changing the price, which leaves nothing to compare against. Vouchers (specs/069) take money off at checkout,
but only for whoever has a code; the listing and the product page say nothing. Found in the review of 2026-10-09.

## User Scenarios & Testing

### User Story 1 - A shopper sees what is reduced (Priority: P1)

On the listing and the product page a reduced product shows its compare-at price struck through beside the price, with
the percentage off; the listing can be narrowed to reduced products.

**Why this priority**: it is what the feature is for.

**Independent Test**: give one variant a compare-at price; read the listing and the product: the struck-through price and
the percentage appear; filter "On sale": only that product.

**Acceptance Scenarios**:

1. **Given** a variant priced 990,000₫ with a compare-at of 1,200,000₫, **When** a shopper reads its product, **Then**
   the variant shows 990,000₫ with 1,200,000₫ struck through and "-17%" (17.5%, rounded down).
2. **Given** that variant is the product's cheapest, **When** the listing is read, **Then** the card shows the same.
3. **Given** a compare-at in dong only, **When** the shop is read in dollars, **Then** nothing is struck through - a
   compare-at is never converted (specs/022).
4. **Given** the filter "On sale", **Then** only products with a variant on sale with a compare-at in the chosen currency
   are listed.

---

### User Story 2 - A seller sets and removes a compare-at price (Priority: P1)

Beside each price on the seller's product page there is a compare-at price to set or clear.

**Why this priority**: nothing shows a reduction until a seller sets one.

**Independent Test**: as the seller, set a compare-at above the price, then one at or below it (refused), then clear it.

**Acceptance Scenarios**:

1. **Given** a variant priced in a currency, **When** its seller sets a compare-at above that price, **Then** it is kept
   and shown.
2. **Given** a compare-at at or below the price, not representable in the currency, or in a currency the variant has no
   price in, **Then** it is refused with a message saying why.
3. **Given** a compare-at, **When** the seller lowers the price, **Then** the compare-at stays; **When** the seller
   raises the price to or above it, **Then** the compare-at is cleared in the same change - a reduction that is no
   longer one is not shown.
4. **Given** an approved product, **When** its seller sets or clears a compare-at, **Then** it stays on the shelf: prices
   never send a product back to review (specs/045).
5. **Given** another seller's product, **Then** 404 (specs/027).

### Edge Cases

- **Checkout is untouched**: the order is priced by Catalog's `PriceVariants` as before, and the compare-at is never
  frozen on an order, a quote or a payment.
- **Removing a price** (a non-default currency) removes its compare-at with it.
- **An older image** (rollback) ignores the new columns: prices and checkout work; nothing is struck through.
- **The read cache** evicts on the write (both columns are on tables already in its pattern, specs/157).

## Requirements

### Functional Requirements

- **FR-001**: A variant MUST be able to hold, per currency it is priced in, an optional **compare-at price** strictly
  above its price, representable in that currency.
- **FR-002**: The database MUST refuse a compare-at that is not above its price.
- **FR-003**: Setting a price at or above its compare-at MUST clear the compare-at in the same change; removing a price
  MUST remove its compare-at.
- **FR-004**: The variant response MUST carry `compareAtPrice` in the currency asked for (null when none); the product
  response MUST carry the compare-at of the variant that gives its "from" price.
- **FR-005**: The listing MUST accept `onSale=true`: only products with an active variant with a compare-at in the
  currency asked for.
- **FR-006**: Nothing the shop charges may read the compare-at: checkout, the quote, orders and payments are unchanged.
- **FR-007**: Setting or clearing a compare-at MUST be the seller's (own) or staff's, audited, and MUST NOT send a
  product back to review.
- **FR-008**: The storefront MUST show the struck-through compare-at and the percentage off on cards and the product
  page, offer an "On sale" filter, and let the seller set and clear it beside each price.
- **FR-009**: The schema change MUST be expand-only.

### Key Entities

- **Compare-at price**: what a variant's price is compared against, in one currency - beside that currency's price.

## Success Criteria

- **SC-001**: A variant with a compare-at reads back struck through with the right percentage, on the product and on the
  card, in a browser.
- **SC-002**: A compare-at not above its price is refused by the command and by the database; raising the price clears
  it; tested on PostgreSQL, with the clearing shown by mutation.
- **SC-003**: `onSale` lists exactly the reduced products in the currency asked for.
- **SC-004**: A checkout of a reduced variant charges its price; the quote and the order carry no compare-at.
- **SC-005**: Bruno covers the endpoints and their refusals; `docs/reference` regenerated.

## Assumptions

- The percentage is rounded down to a whole number (never overstating a reduction), and computed by the storefront.
- A compare-at says nothing about when a reduction ends; scheduled sales are out of scope.
- The seed sets a few compare-at prices so the shop shows reductions; like its prices, they are approximate.
