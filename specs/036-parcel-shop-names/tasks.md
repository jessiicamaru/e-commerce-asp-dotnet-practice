---
description: "Task list for Which shop each parcel comes from"
---

# Tasks: Which shop each parcel comes from

> Completed on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Story labels and paths were added to the existing tasks;
> T011 onward were added in this backfill.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel; **[Story]**: US1 who sends each parcel, US2 the name at the time of purchase

- [X] T001 [US1] Proto: `optional string seller_name = 10` on `PricedVariant` in server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto
- [X] T002 [P] [US1] Catalog test: a seller's variant is priced with the shop name; the shop's own has none; an unknown seller has no name rather than a placeholder - server/tests/Ecommerce.Catalog.Tests/VariantSellerPricingTests.cs
- [X] T003 [US1] Catalog: `CatalogPricingService` takes `ISellerRepository`, fills `seller_name` in `PriceVariants` and `DescribeVariants`
- [X] T004 [US2] Order: `OrderItem.SellerName`, configuration, migration `AddOrderItemSellerName` (server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/20260923122805_AddOrderItemSellerName.cs)
- [X] T005 [US2] Order: `CatalogPrice.SellerName`, `PricedLine.SellerName`, frozen by `SubmitOrderCommandHandler`; `GrpcCatalogPrices` maps unset/empty to null
- [X] T006 [US1] Order responses: `SellerName` on order lines and quote lines; `SellerName` + `IsShop` on parcels
- [X] T007 [US1] [US2] Order tests: frozen at checkout; a later rename (a different name from Catalog) does not change it; unknown is null; parcels carry name and isShop
- [X] T008 [P] [US1] Client: types; "Sold by" on order lines; shop name / "The shop" on parcel headings; strings; Vitest tests
- [X] T009 [US2] Run everything; end to end: two-seller order, rename a shop, re-read the order; verify-saga.sh; Bruno
- [X] T010 CLAUDE.md

> **Tests first, this time on the server.** The Catalog test for the name and all five Order tests were
> written and seen RED against plumbing that compiled but did not carry the name, then made green.
> The client code preceded its tests. The end to end first recorded a rename check that could pass
> before the rename had reached Catalog; it was re-run waiting until the catalogue showed the new
> name, and the order still showed the old one.

## Added in the 2026-09-27 backfill (work the pull request shows)

- [X] T011 [US1] `CheckoutPricing` and `GetCheckoutQuoteQuery` carry the name so the quote names the shop before payment (server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/CheckoutPricing.cs)
- [X] T012 [P] [US1] `An_absent_or_empty_name_on_the_wire_is_no_name` in server/tests/Ecommerce.Order.Tests/ShopNameTests.cs - the fifth Order test, on `GrpcCatalogPrices.SellerNameOf`
- [X] T013 [P] [US1] Client tests in client/src/components/order/order-lines/index.test.tsx (names the shop; nothing for a line with none; translated) and order-shipments/index.test.tsx (names each parcel's shop and "the shop"; nothing when no name was recorded - never "from null")
- [X] T014 The design record completed to the specs/001 standard: data-model.md, quickstart.md, plan structure, research labels (2026-09-27)
- [X] T015 Merged as **#80** (`cd391d2`) on 2026-09-23: Catalog 135, Order 103, all backend 357, client 109 tests; Bruno 103/103 requests, 164/164 tests; `verify-saga.sh` green; end to end with a rename that waited for the catalogue
