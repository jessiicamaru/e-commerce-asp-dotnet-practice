# Tasks: A seller can stock what they sell

## Phase 1: The contract

- [ ] T001 `catalog_ownership.proto` in server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/
- [ ] T002 Register it in Ecommerce.Contracts.Grpc.csproj (server and client)
- [ ] T003 `CatalogOwnershipService` in server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/
- [ ] T004 `MapGrpcService<CatalogOwnershipService>()` in Catalog's Program.cs
- [ ] T005 Catalog tests: reports the seller id; reports empty for a shop-owned product; omits a variant that does not exist

## Phase 2: Inventory learns who is calling

- [ ] T006 `IProductOwnership` in server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/Interfaces/
- [ ] T007 gRPC implementation in Ecommerce.Inventory.Infrastructure/, registered in its DependencyInjection
- [ ] T008 `Catalog:GrpcUrl` in Inventory's appsettings.json and docker-compose.app.yml
- [ ] T009 Add the Grpc packages and the project reference to Inventory's csproj, and the Dockerfile line if one is missing

## Phase 3: The refusal

- [ ] T010 Controller attribute to `"Seller,Admin"` in server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Controllers/StockController.cs
- [ ] T011 Ownership check in SetStockOnHandCommandHandler, BEFORE the transaction opens
- [ ] T012 404 for a variant that is not the caller's, worded exactly as a missing variant
- [ ] T013 The "row has not arrived yet" 404 is reachable only after ownership passes, and says so
- [ ] T014 Inventory tests: owner passes, other seller 404, administrator passes, missing row after ownership
- [ ] T015 Run Inventory and Catalog suites

## Phase 4: The seller console

- [ ] T016 `Stock` service and `useStock` / `useSetStock` hooks in client/src/services/ and hooks/
- [ ] T017 Quantity control on client/src/pages/shop-product/index.tsx, showing on hand and reserved
- [ ] T018 Strings in client/src/locales/vi/seller.json and en/seller.json
- [ ] T019 Vitest tests: what it sends, the reserved figure shown, a refusal in the server's words
- [ ] T020 Remove the "sellers cannot set stock yet" line from the create form's consequences

## Phase 5: End to end

- [ ] T021 Two real sellers against the running stack: SC-002, 404 never 403
- [ ] T022 SC-001: list, stock, buy - on-hand falls by exactly one, nothing left held
- [ ] T023 `verify-saga.sh` and `verify-auth.sh`
- [ ] T024 Bruno: a seller stocking their own, and a security-check for somebody else's

## Phase 6: Say so

- [ ] T025 CLAUDE.md: the new gRPC edge, the rule, the service map row, the test counts
- [ ] T026 PR closing #70
