# Tasks: A seller can see what they sold

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/api.md](contracts/api.md)
**Tests**: requested - the spec's success criteria are refusals, and a refusal is only proven by a test that fails without it.

## Phase 1: Setup

- [X] T001 Confirm the baseline: Order 61, Catalog 130, client 43 pass on `main` before any change

## Phase 2: Foundational - the contract

- [X] T002 Add `optional string seller_id = 9` to `PricedVariant` in server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto
- [X] T003 [P] Catalog test: a seller's variant is priced with its seller, the shop's with an empty-but-present seller, in server/tests/Ecommerce.Catalog.Tests/VariantSellerPricingTests.cs
- [X] T004 Fill `SellerId` in `CatalogPricingService.Describe`, leaving it unset when the product was not loaded, in server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/CatalogPricingService.cs

## Phase 3: US1 - the sale remembers whose it was (P1)

- [X] T005 [US1] Tests first: a seller's line records the seller, a shop line records none, a mixed order records each, in server/tests/Ecommerce.Order.Tests/SellerSalesTests.cs
- [X] T006 [US1] `OrderItem.SellerId` in server/src/Services/Order/Ecommerce.Order.Domain/Entities/OrderItem.cs and its index in server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/OrderItemConfiguration.cs
- [X] T007 [US1] Migration `AddOrderItemSeller` in server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/
- [X] T008 [US1] `CatalogPrice.SellerId` in server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/ICatalogPrices.cs; `PricedLine.SellerId` in server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/CheckoutPricing.cs; frozen in server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs
- [X] T009 [US1] `GrpcCatalogPrices` maps an absent `seller_id` to null and logs a warning naming the variants, in server/src/Services/Order/Ecommerce.Order.Infrastructure/Catalog/GrpcCatalogPrices.cs

## Phase 4: US2 + US3 - a seller reads their sales (P1)

- [X] T010 [US2] Tests first, in server/tests/Ecommerce.Order.Tests/SellerSalesTests.cs: listed only when holding a line of theirs; failed and submitted never listed; legacy Completed reads Paid; subtotal over their lines only; paging and total count; page size validated
- [X] T011 [US3] Tests first, same file: only their lines; another seller's order, a failed one, a settling one and a missing one all throw the same `NotFoundException` message; the response type has no customer, address, total or tracking field
- [X] T012 [US2] Response records `SaleSummaryResponse`, `SaleDetailResponse` in server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/SaleResponses.cs
- [X] T013 [US2] `GetSalesPageAsync` and `GetSaleAsync` on server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/IOrderRepository.cs, implemented in server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs
- [X] T014 [US2] `GetMySalesQuery` + validator + handler in server/src/Services/Order/Ecommerce.Order.Application/Orders/Queries/GetMySales/
- [X] T015 [US3] `GetMySaleQuery` + handler in server/src/Services/Order/Ecommerce.Order.Application/Orders/Queries/GetMySale/
- [X] T016 [US2] Two `[Authorize(Roles = "Seller")]` routes in server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/OrdersController.cs (declared after `{id:guid}`, which is harmless: `sales` does not satisfy the `:guid` constraint)

> **What "tests first" actually was here.** The Catalog test (T003) was seen red before T004. On the
> Order side the code was written BEFORE `SellerSalesTests`, so its first run being green proved
> nothing. The evidence is a mutation run instead: each of five guards was removed in turn and the
> tests re-run - status filter, subtotal over the seller's lines, detail filtered to the seller's
> lines, detail refusing an order with none of theirs, and the seller frozen at checkout. **Every one
> turned at least one test red**, and the source was restored after each.

## Phase 5: US4 - the storefront (P2)

- [X] T017 [P] [US4] Types `SaleSummary`, `SalePage`, `Sale` in client/src/services/order/types.ts; `Order.sales` and `Order.sale` in client/src/services/order/index.ts
- [X] T018 [P] [US4] Query keys in client/src/constants/query-keys/; `useMySales`, `useSale` in client/src/hooks/order/index.ts
- [X] T019 [US4] Page client/src/pages/shop-sales/index.tsx and client/src/pages/shop-sale/index.tsx; routes in client/src/routes/index.tsx; a link from client/src/pages/shop/index.tsx
- [X] T020 [P] [US4] Strings in client/src/locales/en/seller.json and client/src/locales/vi/seller.json
- [X] T021 [US4] Vitest: the service asks the right address with no seller in it; the list links each sale and shows an empty state; the sale shows its lines and subtotal in the order's currency; a 404 is shown as the server worded it - client/src/services/order/index.test.ts, client/src/pages/shop-sales/index.test.tsx, client/src/pages/shop-sale/index.test.tsx

## Phase 6: Polish & cross-cutting

- [X] T022 [P] Bruno: bruno/seller/ "a seller lists their sales", "an order that is not their sale is 404"; bruno/security-checks/ "a customer cannot list sales is 403", "sales without a token is 401"
- [X] T023 Run everything: Order, Catalog, all backend suites, client lint/test/build, Bruno headless
- [X] T024 End to end on the running stack per quickstart.md, including a second seller and `verify-saga.sh`
- [X] T025 [P] CLAUDE.md: the paragraph (frozen here vs live in specs/031), test counts, service map note
- [X] T026 File the follow-up issue: per-seller fulfilment (#76)
- [X] T027 Found on a phone-width screenshot, not planned: `OrderLines` overflowed its container by 14px (372 in 358), scrolling the line total's currency sign out of sight. The name cell now wraps, in client/src/components/order/order-lines/index.tsx - which fixes the customer's order page too

## Dependencies

T002 → T004 → (T008, T009). T005 before T006-T009. T010/T011 before T012-T016. Phase 5 needs T016's
contract only, so it can start once the routes are shaped. T023-T024 last.

## MVP

Phases 2-4: a seller can ask, through the API, what they sold. The storefront is the P2 increment.
