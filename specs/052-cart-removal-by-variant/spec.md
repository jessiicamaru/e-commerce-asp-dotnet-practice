# Feature Specification: Cart removal by variant

**Feature Branch**: `052-cart-removal-by-variant` | **Created**: 2026-09-24 | **Issue**: #122

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

## User Scenarios

### US1 - The line that was bought is the line that goes (P1)

**Acceptance**
1. A cart holds two variants of one product. After an order for one of them completes, that line goes
   down by the quantity ordered, and the other line is untouched.
2. Everything specs/010 guarantees still holds:
   - removal happens once;
   - it happens only on completion;
   - it works whichever of the two events arrives first;
   - it is a decrement, so anything added during checkout survives.

### US2 - What was recorded before still applies (P1)

**Acceptance**
1. An order recorded before this change, whose items were stored without a variant, and an item from an
   Order image older than specs/020 (`VariantId = Guid.Empty`) are both matched by product id, as today.
   For such an order that is the only variant: the first variant of a product reuses the product's id
   (specs/020).
2. A cart line written before variants existed (`VariantId` null) is matched through `SellableId`, which
   falls back to the product id.

## Requirements

- **FR-001**: `OrderedItem` carries the variant, mapped from the event in one place
  (`OrderedItem.From`) that a test covers.
- **FR-002**: Removal matches `CartLine.SellableId` against the item's variant, or against its product
  id when it names no variant.
- **FR-003**: No migration, and no contract change. The stored JSON only gains a field, and JSON
  written before this still reads.
