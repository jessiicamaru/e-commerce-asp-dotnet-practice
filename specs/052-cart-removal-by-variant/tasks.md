---
description: "Task list for Cart removal by variant"
---

# Tasks: Cart removal by variant

> Completed on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the two-variants test failed before the fix; the three legacy guards
passed before and after, as they should.

## Format: `[ID] [P?] [Story] Description`

- [X] T001 [US1] [US2] Tests first in `server/tests/Ecommerce.Cart.Tests/VariantLineTests.cs`: two variants, one ordered; legacy item without a variant; legacy line without a variant; `OrderedItem.From` carries the variant
- [X] T002 [US1] `OrderedItem` with `VariantId`, `Sellable`, `From` and the match on `SellableId` in `server/src/Services/Cart/Ecommerce.Cart.Application/Checkout/CheckoutOutcomes.cs`; the consumer in `server/src/Services/Cart/Ecommerce.Cart.WebApi/Consumers/OrderSubmittedConsumer.cs`
- [X] T003 Mutation checks; docs `docs/features/shopping-and-checkout.md`, `docs/project/*`
- [X] T004 Merged as #134 (2026-09-24), closing #122

## Verification recorded in #134

- `Ecommerce.Cart.Tests` 18/18 against real PostgreSQL (14 before, plus 4 new).
- Mutations, each restored: match back on `ProductId` - 1 red; no fallback for an empty variant - 8 red;
  `From` drops the variant - 1 red.

## Notes

T004 was added on 2026-09-27 to name the merge.
