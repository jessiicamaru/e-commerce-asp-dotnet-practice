# Feature Specification: Cart removal by variant

> Completed on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**Feature Branch**: `052-cart-removal-by-variant` | **Created**: 2026-09-24 | **Issue**: #122

**Status**: Merged (#134, 2026-09-24)

**Input**: Issue #122 - when an order completes, Cart removes the ordered quantity from a line matched by
product, so with two variants of one product in the cart the wrong line can go.

## Why

When an order completes, Cart takes what was ordered out of the customer's cart (specs/010). It finds
the line by **product**: `l.ProductId == item.ProductId` in `CheckoutOutcomes.TryApplyAsync`. Since
specs/020 a cart holds **variants**, and every other cart path matches on the variant
(`CartLine.SellableId`).

Take a customer with a lens in Sony E mount and the same lens in Fujifilm X mount in their cart. They
buy the Fujifilm one. The decrement lands on whichever line comes first: the Sony line goes down, and
the Fujifilm one they bought stays in the cart.

The variant was never missing. `OrderSubmittedEvent`'s items have carried `VariantId` since specs/020.
`OrderSubmittedConsumer` dropped it when it built `OrderedItem`.

## User Scenarios & Testing *(mandatory)*

### US1 - The line that was bought is the line that goes (Priority: P1)

A customer with two variants of one product in the cart buys one. After payment the bought line goes down
by the quantity ordered; the other is untouched.

**Why this priority**: It is the defect. The customer loses the thing they did not buy from their cart and
keeps the thing they did - and nothing reports it.

**Independent Test**: Put two variants of one product in a cart, record a submission for one of them and its
completion, and read the cart.

**Acceptance Scenarios**:

1. A cart holds two variants of one product. After an order for one of them completes, that line goes
   down by the quantity ordered, and the other line is untouched.
2. Everything specs/010 guarantees still holds:
   - removal happens once;
   - it happens only on completion;
   - it works whichever of the two events arrives first;
   - it is a decrement, so anything added during checkout survives.

---

### US2 - What was recorded before still applies (Priority: P1)

Orders submitted before this change are waiting in `checkout_outcomes` with items stored without a variant;
an Order image older than specs/020 sends none; and cart lines written before variants have none either.
All of them must still be removed correctly.

**Why this priority**: Equal to US1 - a fix that strands orders already in flight would trade one silent
failure for another.

**Independent Test**: Store an item without a variant (or with `Guid.Empty`) against a cart whose line is the
product's first variant; complete the order; the line goes down.

**Acceptance Scenarios**:

1. An order recorded before this change, whose items were stored without a variant, and an item from an
   Order image older than specs/020 (`VariantId = Guid.Empty`) are both matched by product id, as today.
   For such an order that is the only variant: the first variant of a product reuses the product's id
   (specs/020).
2. A cart line written before variants existed (`VariantId` null) is matched through `SellableId`, which
   falls back to the product id.

### Edge Cases

- **The bought line was removed during checkout.** Nothing to decrement; the item is skipped, as before.
- **The customer added more of the bought variant during checkout.** A decrement, not a removal: the extra
  survives (specs/010).
- **Completion before submission.** Whichever arrives second applies, under the `Applied` flag (specs/010).
- **Redelivery.** `Applied` under `FOR UPDATE` decides "once"; unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `OrderedItem` carries the variant, mapped from the event in one place
  (`OrderedItem.From`) that a test covers.
- **FR-002**: Removal matches `CartLine.SellableId` against the item's variant, or against its product
  id when it names no variant.
- **FR-003**: No migration, and no contract change. The stored JSON only gains a field, and JSON
  written before this still reads.

### Key Entities

- **Ordered item**: what Cart remembers of one order line between submission and completion - product,
  quantity and, since this feature, variant. Stored as JSON in `checkout_outcomes.ItemsJson`.
- **Cart line**: one variant in a customer's cart; `SellableId` is its variant id, or its product id when it
  was written before variants.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With two variants of one product in the cart and one ordered, the ordered line - and only it -
  goes down, verified by a test that failed before the fix.
- **SC-002**: The existing Cart tests (14) still pass, alongside the new ones.
- **SC-003**: An item or a cart line with no variant is still matched, verified by three legacy tests that pass
  both before and after the fix.

## Assumptions

- The first variant of every product reuses the product's id (specs/020), so a product id is an exact
  variant id for anything recorded without one.

## Out of scope

- Any change to the contracts, to Order, or to the other cart paths, which already matched by variant.
