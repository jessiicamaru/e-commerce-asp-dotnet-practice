# Tasks: Which shop each parcel comes from

- [ ] T001 Proto: `optional string seller_name = 10` on `PricedVariant`
- [ ] T002 Catalog test: a seller's variant is priced with the shop name; the shop's own has none; names are looked up once per request - server/tests/Ecommerce.Catalog.Tests/VariantSellerPricingTests.cs
- [ ] T003 Catalog: `CatalogPricingService` takes `ISellerRepository`, fills `seller_name` in `PriceVariants` and `DescribeVariants`
- [ ] T004 Order: `OrderItem.SellerName`, configuration, migration `AddOrderItemSellerName`
- [ ] T005 Order: `CatalogPrice.SellerName`, `PricedLine.SellerName`, frozen by `SubmitOrderCommandHandler`; `GrpcCatalogPrices` maps unset/empty to null
- [ ] T006 Order responses: `SellerName` on order lines and quote lines; `SellerName` + `IsShop` on parcels
- [ ] T007 Order tests: frozen at checkout; a later rename (a different name from Catalog) does not change it; unknown is null; parcels carry name and isShop
- [ ] T008 Client: types; "Sold by" on order lines; shop name / "The shop" on parcel headings; strings; Vitest tests
- [ ] T009 Run everything; end to end: two-seller order, rename a shop, re-read the order; verify-saga.sh; Bruno
- [ ] T010 CLAUDE.md
