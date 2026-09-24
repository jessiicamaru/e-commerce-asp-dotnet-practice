# Tasks: Cart removal by variant

- [ ] T001 [US1] [US2] Tests first in `server/tests/Ecommerce.Cart.Tests/VariantLineTests.cs`: two variants, one ordered; legacy item without a variant; legacy line without a variant; `OrderedItem.From` carries the variant
- [ ] T002 [US1] `OrderedItem` with `VariantId`, `Sellable`, `From` and the match on `SellableId` in `server/src/Services/Cart/Ecommerce.Cart.Application/Checkout/CheckoutOutcomes.cs`; the consumer in `server/src/Services/Cart/Ecommerce.Cart.WebApi/Consumers/OrderSubmittedConsumer.cs`
- [ ] T003 Mutation checks; docs `docs/features/shopping-and-checkout.md`, `docs/project/*`
