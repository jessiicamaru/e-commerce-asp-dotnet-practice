# Implementation Plan: Cart removal by variant

> Completed on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**Branch**: `052-cart-removal-by-variant` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #122

## Summary

Carry the variant that `OrderSubmittedEvent` already has into Cart's `OrderedItem`, map it in one place
(`OrderedItem.From`), and match the cart line on `SellableId` instead of `ProductId`. An item with no variant
falls back to the product id, which is exactly the first variant's id. Two source files; no contract, no
migration.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (the existing `OrderSubmittedConsumer`), `System.Text.Json`
(`[JsonIgnore]` on the derived property), `Ecommerce.Contracts.Order.OrderItemDto`

**Storage**: PostgreSQL, `ecommerce_cart_db` (5439) - the existing `checkout_outcomes.ItemsJson` (`jsonb`) and
`cart_lines`

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Cart.Tests`), then three mutations

**Target Platform**: Cart service (5062)

**Constraints**: orders already waiting in `checkout_outcomes` must still apply; specs/010's once-only,
either-order, decrement semantics unchanged

**Scale/Scope**: one record, one match, one consumer line

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

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- III (idempotence): unchanged. The `Applied` flag, under `FOR UPDATE`, still decides "once". Pass.
- V (evidence): the two-variants test fails before the fix. Mutation checks follow. Pass.
- No contract, migration or cross-service change. `OrderItemDto` already carries `VariantId`.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Cart reads only its own tables and the event it already consumed; `OrderItemDto` in `Ecommerce.Contracts` is unchanged |
| **II. Clean Architecture Layering** | **Pass.** The match is in `Application/Checkout/CheckoutOutcomes.cs`; the consumer in WebApi only maps the message, through `OrderedItem.From` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Unchanged: each event is handled under `FOR UPDATE` on the order's `checkout_outcomes` row, and the removal happens only while `Applied` is false, in the transaction that sets it |
| **IV. Identity Comes From the Token** | **Pass, not engaged.** No endpoint; the cart is identified by the `UserId` on the submitted event, as before |
| **V. Evidence Over Assumption** | **Pass.** The two-variants test failed before the fix (the sibling line went down); three legacy guards passed before and after; three mutations were each caught; all against a real PostgreSQL |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/052-cart-removal-by-variant/
├── spec.md
├── plan.md            # This file
├── research.md        # D1-D3
├── data-model.md      # The stored JSON gains a field; no migration
├── quickstart.md
├── contracts/
│   └── messages.md    # OrderSubmittedEvent: VariantId now read, contract unchanged
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/Services/Cart/Ecommerce.Cart.Application/Checkout/CheckoutOutcomes.cs` - `OrderedItem`, the match
- `server/src/Services/Cart/Ecommerce.Cart.WebApi/Consumers/OrderSubmittedConsumer.cs` - `OrderedItem.From`
- `server/tests/Ecommerce.Cart.Tests/VariantLineTests.cs` - four new tests
- `docs/features/shopping-and-checkout.md`, `docs/project/backlog.md`, `docs/project/timeline.md`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

Nothing is left open by it. It does not revisit the other cart paths, which already matched by variant.
