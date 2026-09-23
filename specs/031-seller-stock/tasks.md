# Tasks: A seller can stock what they sell

## Phase 1: The contract

- [X] T001 `catalog_ownership.proto` in server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/
- [X] T002 Register it in Ecommerce.Contracts.Grpc.csproj (server and client)
- [X] T003 `CatalogOwnershipService` in server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/
- [X] T004 `MapGrpcService<CatalogOwnershipService>()` in Catalog's Program.cs
- [X] T005 Catalog tests: reports the seller id; reports empty for a shop-owned product; omits a variant that does not exist

## Phase 2: Inventory learns who is calling

- [X] T006 `IProductOwnership` in server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/Interfaces/
- [X] T007 gRPC implementation in Ecommerce.Inventory.Infrastructure/, registered in its DependencyInjection
- [X] T008 `Catalog:GrpcUrl` in Inventory's appsettings.json and docker-compose.app.yml
- [X] T009 Add the Grpc packages and the project reference to Inventory's csproj, and the Dockerfile line if one is missing

## Phase 3: The refusal

- [X] T010 Controller attribute to `"Seller,Admin"` in server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Controllers/StockController.cs
- [X] T011 Ownership check in SetStockOnHandCommandHandler, BEFORE the transaction opens
- [X] T012 404 for a variant that is not the caller's, worded exactly as a missing variant
- [X] T013 The "row has not arrived yet" 404 is reachable only after ownership passes, and says so
- [X] T014 Inventory tests: owner passes, other seller 404, administrator passes, missing row after ownership
- [X] T015 Run Inventory and Catalog suites

## Phase 4: The seller console

- [X] T016 `Stock` service and `useStock` / `useSetStock` hooks in client/src/services/ and hooks/
- [X] T017 Quantity control on client/src/pages/shop-product/index.tsx, showing on hand and reserved
- [X] T018 Strings in client/src/locales/vi/seller.json and en/seller.json
- [X] T019 Vitest tests: what it sends, the reserved figure shown, a refusal in the server's words
- [X] T020 Remove the "sellers cannot set stock yet" line from the create form's consequences

## Phase 5: End to end

- [X] T021 Two real sellers against the running stack: SC-002, 404 never 403
- [X] T022 SC-001: list, stock, buy - on-hand falls by exactly one, nothing left held
- [X] T023 `verify-saga.sh` and `verify-auth.sh`
- [X] T024 Bruno: a seller stocking their own, and a security-check for somebody else's

## Phase 6: Say so

- [X] T025 CLAUDE.md: the new gRPC edge, the rule, the service map row, the test counts
- [X] T026 PR closing #70

## Added while building

- [X] T027 The domain record had to be renamed `VariantOwnership`: the generated proto message is
      already called `VariantOwner`, and the gRPC service needs both in one file
- [X] T028 Inventory's compose block gained `CATALOG_GRPC_ADDRESS` and `depends_on: catalog`
- [X] T029 Bruno: the seller stock request lives in the `seller` folder, not `inventory` - folders
      run in NAME order, so `sellerProductId` is empty in `inventory` and the URL becomes
      `/api/stock/`, whose routing 404 looked like a passing security test. It measured exactly that
- [X] T030 The `w-12` label width was copied from the currency row, where the label is "VND"; the
      Vietnamese for "on hand" wrapped onto three lines. Seen in a screenshot
