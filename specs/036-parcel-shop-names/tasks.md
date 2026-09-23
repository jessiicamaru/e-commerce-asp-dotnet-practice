# Tasks: Which shop each parcel comes from

- [X] T001 Proto: `optional string seller_name = 10` on `PricedVariant`
- [X] T002 Catalog test: a seller's variant is priced with the shop name; the shop's own has none; the shop's own has no name, an unknown seller has no name rather than a placeholder - server/tests/Ecommerce.Catalog.Tests/VariantSellerPricingTests.cs
- [X] T003 Catalog: `CatalogPricingService` takes `ISellerRepository`, fills `seller_name` in `PriceVariants` and `DescribeVariants`
- [X] T004 Order: `OrderItem.SellerName`, configuration, migration `AddOrderItemSellerName`
- [X] T005 Order: `CatalogPrice.SellerName`, `PricedLine.SellerName`, frozen by `SubmitOrderCommandHandler`; `GrpcCatalogPrices` maps unset/empty to null
- [X] T006 Order responses: `SellerName` on order lines and quote lines; `SellerName` + `IsShop` on parcels
- [X] T007 Order tests: frozen at checkout; a later rename (a different name from Catalog) does not change it; unknown is null; parcels carry name and isShop
- [X] T008 Client: types; "Sold by" on order lines; shop name / "The shop" on parcel headings; strings; Vitest tests
- [X] T009 Run everything; end to end: two-seller order, rename a shop, re-read the order; verify-saga.sh; Bruno
- [X] T010 CLAUDE.md

> **Tests first, this time on the server.** The Catalog test for the name and all five Order tests were
> written and seen RED against plumbing that compiled but did not carry the name, then made green.
> The client code preceded its tests. The end to end first recorded a rename check that could pass
> before the rename had reached Catalog; it was re-run waiting until the catalogue showed the new
> name, and the order still showed the old one.
