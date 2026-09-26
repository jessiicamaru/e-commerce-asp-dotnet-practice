# Research: Cart removal by variant

> Written on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

---

## D1 - Match on `SellableId`, like every other cart path

**Decision**: `cart.Lines.FirstOrDefault(l => l.SellableId == item.Sellable)` in `TryApplyAsync`.

**Rationale**: Since specs/020 a cart line is a variant, and adding, setting a quantity, removing and reading
the cart all match `SellableId` (`VariantId ?? ProductId`). Removal on completion was the one path still
matching `ProductId`, which picks whichever line of a product comes first.

**Alternatives considered**:

- **Keep matching by product.** Rejected: that is #122.
- No other alternative is recorded.

---

## D2 - An item naming no variant means the product's first variant

**Decision**: `OrderedItem.Sellable => VariantId == Guid.Empty ? ProductId : VariantId`, with `VariantId`
defaulting to `Guid.Empty`.

**Rationale**: The first variant of every product reuses the product's id (specs/020), so a product id is a
real variant id: the first variant's. An item that names no variant came from an order placed when each
product had exactly one shape, and that shape's id is the product id. The same reasoning is behind Inventory
reading `Guid.Empty` as the product id (specs/020 research D2). The fallback is therefore exact, not merely
compatible.

`Guid.Empty` covers both legacy sources: an Order older than specs/020 sends it, and JSON stored in
`checkout_outcomes.ItemsJson` before this change has no `VariantId` field and deserialises to it.

**Alternatives considered**:

- **Backfill the stored JSON with variant ids.** Not recorded as considered; the fallback made it unnecessary,
  and FR-003 rules out a migration.

---

## D3 - One mapping from the event, `OrderedItem.From`

**Decision**: `public static OrderedItem From(OrderItemDto item) => new(item.ProductId, item.Quantity,
item.VariantId)`, called by `OrderSubmittedConsumer` as `(m.Items ?? []).Select(OrderedItem.From)`.

**Rationale**: The consumer's inline `new OrderedItem(i.ProductId, i.Quantity)` is where the variant was
dropped. A named mapping that a test covers
(`The_variant_travels_from_the_event_and_an_empty_one_falls_back_to_the_product`) stops a future field being
dropped the same way; the mutation "`From` drops the variant" turned it red.

`Sellable` is `[JsonIgnore]` so the stored JSON holds only the facts (`ProductId`, `Quantity`, `VariantId`),
not a value derived from them.

**Alternatives considered**: none recorded.
