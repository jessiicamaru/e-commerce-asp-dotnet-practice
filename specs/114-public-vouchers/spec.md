# Feature Specification: Shoppers see the vouchers they could use

**Feature Branch**: `114-public-vouchers` | **Created**: 2026-10-01 | **Issue**: #220

**Status**: Draft

**Input**: Issue #220 - "a shopper cannot see which vouchers they could use": "Mark a voucher public (shown) or private
(code only). Public vouchers on the shop page, the product page and at checkout - only ones usable in the shopper's
currency and not yet used up."

## Why

A voucher reaches a shopper only if somebody tells them its code. A seller who makes one for their shop has nowhere to
show it; the platform's own promotions are invisible unless posted elsewhere. Some codes should stay private (a code
handed to one customer), so showing has to be the owner's choice.

## User Scenarios & Testing *(mandatory)*

### US1 - An owner chooses to show a voucher (Priority: P1)

When a seller or administrator creates a voucher they tick "Show it to shoppers"; they can change it later with the
voucher's other terms (specs/113). A voucher is private unless shown - every voucher from before stays code-only.

**Acceptance Scenarios**:

1. **Given** a new voucher created without the tick, **Then** it is private; with it, public.
2. **Given** an active voucher, **When** its owner edits it to be shown or hidden, **Then** the lists follow at once.
3. **Given** the voucher list, **Then** each voucher says whether it is shown or code-only.

---

### US2 - A shopper sees the shop's vouchers on its page (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a shop with a public voucher and a private one, **When** anybody opens the shop's page, **Then** the public
   one is listed with its code and what it gives, and the private one never is.
2. **Given** a public voucher that has ended, not started, been disabled, been used up, or has no amount in the currency
   being browsed in, **Then** it is not listed.
3. **Given** a signed-out visitor, **Then** they see the same list: showing a voucher is not a secret.

---

### US3 - On the product page and at checkout (Priority: P2)

**Acceptance Scenarios**:

1. **Given** a product, **Then** its page lists the public vouchers that could apply to it: the platform's and its
   shop's, each either for everything or naming this product or one of its variants.
2. **Given** a checkout, **Then** it lists the platform's public vouchers and those of each shop in the cart, and
   "Use" on one tries it exactly as typing its code does - the server's answer decides (specs/070).

### Edge Cases

- **Limits and uses are not shown.** A public list says what a voucher gives and until when - not how many are left,
  which is the owner's business. "Not used up" is a filter, not a number.
- **Per-customer limits and "new customers only"** are not judged by the list, which may be anonymous: the conditions
  are worded, and the checkout's quote refuses a voucher the shopper cannot use, in its own words.
- **Many vouchers**: at most 12 are listed, ending soonest first - the ones about to go are the ones worth seeing.
- **A shop closed or paused**: its product pages and shop page are already 404 (specs/107), so its vouchers are not
  reached there; at checkout its goods cannot be in a cart that prices.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `vouchers.IsPublic` (false for every existing voucher); `isPublic` on create and on the edit of specs/113.
- **FR-002**: `GET /api/vouchers/public` - anonymous - with `sellerId` (repeatable), `platform`, `productId` and
  `variantId` (repeatable): active, started, not ended, not used up, priced in the request's currency, public; the
  platform's if asked and each named shop's; with `productId`, only those for everything or targeting that product or
  one of the given variants. At most 12, ending soonest first (open-ended last).
- **FR-003**: Each listed voucher carries its code, name, whose (platform or which shop), benefit, percent, the amount,
  cap and minimum in the request's currency, its end and its conditions - never a limit or a count.
- **FR-004**: The checkout quote's lines carry the seller id, so the page can ask for the vouchers of the shops in the
  cart.
- **FR-005**: The shop page, the product page and the checkout show the list; checkout's "Use" applies through the
  existing voucher box.

## Success Criteria *(mandatory)*

- **SC-001**: A public shop voucher appears on that shop's page; a private one never does (tested on the server and the
  page).
- **SC-002**: Nothing ended, not started, disabled, used up or unpriced in the currency is ever listed (tested).
- **SC-003**: The list carries no limit or use count (the contract; tested).

## Assumptions

- Visibility is not a price term, so changing it through the edit of specs/113 is within that feature's rule.
- The list is read per page view and not cached beyond the request (`Vary: X-Currency` as every priced read).
