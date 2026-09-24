# Implementation Plan: Cart removal by variant

**Branch**: `052-cart-removal-by-variant` | **Spec**: [spec.md](spec.md) | **Issue**: #122

## Design

- `OrderedItem(Guid ProductId, int Quantity, Guid VariantId = default)`:
  - It has a `[JsonIgnore] Sellable` property: the variant, or the product when there is no variant.
    `Guid.Empty` is what a pre-specs/020 Order sends, and what an item stored before this deserialises
    to.
  - It has a `static From(OrderItemDto)`, the one mapping from the event.
- `TryApplyAsync` matches `l.SellableId == item.Sellable`.
- `OrderSubmittedConsumer` calls `OrderedItem.From`.

## Why the fallback is right, not merely compatible

The first variant of every product reuses the product's id (specs/020). So a product id is a real
variant id: the first variant's. An item that names no variant came from an order placed when each
product had exactly one shape, and that shape's id is the product id. The same reasoning is behind
Inventory reading `Guid.Empty` as the product id (specs/020 research D2).

## Constitution check

- III (idempotence): unchanged. The `Applied` flag, under `FOR UPDATE`, still decides "once". Pass.
- V (evidence): the two-variants test fails before the fix. Mutation checks follow. Pass.
- No contract, migration or cross-service change. `OrderItemDto` already carries `VariantId`.
