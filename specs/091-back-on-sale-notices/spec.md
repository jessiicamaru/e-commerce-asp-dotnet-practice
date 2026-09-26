# Feature Specification: A saved product back on sale by any route tells whoever saved it

**Feature Branch**: `091-back-on-sale-notices` | **Created**: 2026-09-27 | **Issue**: #182

**Status**: Draft

**Input**: Issue #182 - "a saved product back on sale by any route but Inventory's tells nobody".

## Why

Specs/075 tells a shopper who saved a product when it comes back in stock. It does so only when **Inventory's**
availability announcement flips the product's rollup from unavailable to available:
`RecordStockAvailabilityCommandHandler` uses the flip that `RecomputeProductRollupAsync` returns. Three other
handlers call the same recompute and **discard** its answer:

| Handler | What can flip | Before |
| :-- | :-- | :-- |
| `UpdateProductVariantCommand` | reactivating a variant that has stock | nobody told |
| `AddProductVariantCommand` | a recompute over a stale rollup | nobody told |
| `SetVariantPriceCommand` | a recompute over a stale rollup | nobody told |

And one route puts a product back on the shelf without touching the rollup at all: a moderator **approving** it.
That is how a product returns after a seller's edit sent it back to review (specs/045) or after a take-down and a
resubmission. For somebody who saved it while it was listed, it was "no longer available" in their list (specs/075)
and now can be bought again - and nobody told them.

A survey of the code (research D3) found no command that sets a **product's** own `IsActive`, so there is no
"reactivated product" route; and a price does not enter the rollup (see Decision), so a price alone does not flip it.

## User Scenarios & Testing *(mandatory)*

### US1 - A variant reactivated puts the product back, and savers are told (Priority: P1)

A seller or administrator deactivates the only variant of a product that has stock - the product is unavailable -
and later reactivates it. Everybody who saved the product is told, once, by notice and email, as for Inventory's
route.

**Why this priority**: The commonest non-Inventory route, and a seller's own action.

**Independent Test**: Save a product, make it available (told once), deactivate its variant, reactivate it: the saver
has two notices and two emails.

**Acceptance Scenarios**:

1. **Given** a saved product in stock whose only variant is deactivated, **When** it is reactivated, **Then** each
   saver gets `SavedBackInStock` (notice and email) with a link to the product.
2. **Given** the reactivation, **Then** the notices commit in the same transaction as the variant change and the
   rollup - both or neither.

---

### US2 - A moderator's approval puts a product back, and savers are told (Priority: P1)

A product that was listed, saved and in stock went back to review. When a moderator approves it and it is in stock,
its savers are told. When it is out of stock, nobody is told - there is nothing to buy, and Inventory's route will
tell them when units arrive.

**Why this priority**: Equal to US1; it is the only route that does not go through the rollup at all.

**Independent Test**: Save a product, make it available, set it `Pending`, approve it as a moderator: two notices.
Repeat with no stock: none.

**Acceptance Scenarios**:

1. **Given** a saved, in-stock product awaiting review, **When** a moderator approves it, **Then** each saver is told.
2. **Given** a saved product with nothing in stock awaiting review, **When** it is approved, **Then** nobody is told.
3. **Given** two moderators approving at once, **Then** only the one whose guarded move won tells anybody (the other
   is the existing 409).

---

### US3 - A change that leaves it on sale tells nobody again (Priority: P1)

A price change, an edit of an active variant, or adding a variant to a product already on sale does not tell anybody:
only the flip from not-buyable to buyable is news (specs/075's rule, kept on every route).

**Why this priority**: Without it the fix would spam savers on every edit - the failure mode the flip rule exists for.

**Independent Test**: Save a product, make it available (told once), change a variant's price, set a price in the
default currency, add a variant: still one notice.

**Acceptance Scenarios**:

1. **Given** a saved product on sale, **When** any of those edits is made, **Then** no new notice or email.

---

### US4 - The words fit both routes (Priority: P3)

The notice, its admin label and the email say the product is **available again**, not "back in stock": after an
approval it may have been in stock all along. The kind keeps its name, `SavedBackInStock`.

**Why this priority**: Wording only, but a notice that says "back in stock" about something that never left stock is
a small lie.

**Independent Test**: The client suite and the email template tests pass with the new default words.

**Acceptance Scenarios**:

1. **Given** the notice in English, **Then** it reads "“{product}”, which you saved, is available again"; in
   Vietnamese "… đã có thể mua lại"; the email's subject and body say the same.

### Edge Cases

- **Two routes at once.** Inventory's announcement and a reactivation racing: each recompute is one statement whose
  CTE reads the value it started from, so exactly one sees the flip and tells.
- **A first approval.** A product never listed cannot have been saved (specs/075), so the saver list is empty.
- **Approved but the product's own `IsActive` is false.** No command sets it false today; if one does, nobody is told.
- **A product off the shelf whose variant is reactivated.** The rollup may flip, but it is not listed: nobody is told,
  as on Inventory's route; the approval will tell them later if it is then in stock.
- **An edit inside a consumer.** None of these handlers runs in one, but the transactional method joins an open
  transaction rather than opening a second (the "already in a transaction" gotcha).
- **A retry.** The transaction runs inside the execution strategy, as every hand-opened transaction in these services
  must (`EnableRetryOnFailure`).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every handler that recomputes the rollup tells the savers when that recompute flips the product back
  in stock and it is listed and not withdrawn - Inventory's route (unchanged), `UpdateProductVariant`,
  `AddProductVariant` and `SetVariantPrice`.
- **FR-002**: The change, the recompute and the notices commit in one transaction
  (`IProductRepository.SaveAndRecomputeRollupAsync`), with the notices staged before the save (Principle III).
- **FR-003**: Approving a product (`Pending` → `Approved`) tells its savers when the product is active and in stock,
  inside the transaction of the guarded move.
- **FR-004**: No other change - and no repeat of "still on sale" - tells anybody.
- **FR-005**: One helper (`SavedProductNotices.BackOnSaleAsync`) sends the notice and the email for every route, so
  the kind, data keys, link and template cannot drift between routes.
- **FR-006**: The default words of the notice, its admin label and the email say "available again" in both languages.
- **FR-007**: No migration, no new notification kind, no contract change.

### Key Entities

- **Saved product** (`saved_products`, specs/075) - who saved what.
- **Product rollup** (`products.Availability`) - "any active variant is available", recomputed from the variants.
- **On sale** - listed (approved), not withdrawn (`IsActive`), and in stock (`Availability`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The reactivation and approval tests failed before the fix and pass after.
- **SC-002**: The "nothing in stock" and "leaves it on sale" tests pass before and after - the fix tells no one it
  should not.
- **SC-003**: Four mutations - approval never tells, approval ignores stock, the callback runs without a flip, the
  callback never runs - each turn a test red.
- **SC-004**: `Ecommerce.Catalog.Tests` and the storefront suite pass in full.

## Decision

1. **Approval counts as "back".** The issue asked; for a saver it is - their list showed the product as no longer
   available (specs/075), and now they can buy it. Only when it is in stock: otherwise Inventory's route tells them
   when it is ([research.md](research.md) D2).
2. **A price does not count.** The rollup's availability ignores price, and a product priced in one currency and not
   another is on sale to some shoppers and not others; "sellable in your currency" is per shopper, and Catalog does
   not know a saver's currency. The price handler still tells on a flip of the rollup, like every other caller, so
   the rule is uniform (D4).
3. **Reuse `SavedBackInStock`, reword it "available again".** A new kind would need a new email, a new label and a
   new contract line for the same message; renaming the kind would orphan stored notices. Rewording the defaults
   makes the one message true for both routes (D5).

Recorded as decided on the user's behalf.

## Assumptions

- `ProductDeletedEvent` / take-down paths are unchanged; only approval lists a product.
- The notice is worded in the reader's language by the storefront, and the email by Identity (specs/042, 083); an
  administrator's edited wording (specs/077, 078) is not overwritten - only the defaults change.

## Out of scope

- "Price dropped" or "now priced in your currency" notices.
- A digest of several saved products.
