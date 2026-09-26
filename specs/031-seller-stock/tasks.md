---
description: "Task list for A seller can stock what they sell"
---

# Tasks: A seller can stock what they sell

> Completed on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md. Story labels and file paths were added to the existing tasks; tasks
> T031 onward were added in this backfill for work the pull request shows and the list did not name.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

**Tests**: included, and required - an authorization boundary is in the constitution's "cannot be
verified by hand" category, and the specs/027 trap (an attribute making the check unreachable) is only
caught by a test through the real path.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency)
- **[Story]**: US1 stock own, US2 refuse another's, US3 the page, US4 the not-arrived-yet row

## Phase 1: The contract

- [X] T001 [US1] `catalog_ownership.proto` in server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/
- [X] T002 [US1] Register it in Ecommerce.Contracts.Grpc.csproj (server and client)
- [X] T003 [US1] `CatalogOwnershipService` in server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/
- [X] T004 [US1] `MapGrpcService<CatalogOwnershipService>()` in Catalog's Program.cs
- [X] T005 [P] [US2] Catalog tests: reports the seller id; reports empty for a shop-owned product; omits a variant that does not exist (server/tests/Ecommerce.Catalog.Tests/VariantOwnershipTests.cs)

## Phase 2: Inventory learns who is calling

- [X] T006 [US1] `IProductOwnership` in server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/Interfaces/
- [X] T007 [US1] gRPC implementation in Ecommerce.Inventory.Infrastructure/, registered in its DependencyInjection
- [X] T008 [US1] `Catalog:GrpcUrl` in Inventory's appsettings.json and docker-compose.app.yml — *as built the key is `Catalog:GrpcAddress`, falling back to `CATALOG_GRPC_ADDRESS` and `http://localhost:5157`, read in `DependencyInjection.cs`; Inventory's appsettings.json was not changed (see T028)*
- [X] T009 [US1] Add the Grpc packages and the project reference to Inventory's csproj, and the Dockerfile line if one is missing

## Phase 3: The refusal

- [X] T010 [US2] Controller attribute to `"Seller,Admin"` in server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Controllers/StockController.cs
- [X] T011 [US2] Ownership check in SetStockOnHandCommandHandler, BEFORE the transaction opens
- [X] T012 [US2] 404 for a variant that is not the caller's, worded exactly as a missing variant
- [X] T013 [US4] The "row has not arrived yet" 404 is reachable only after ownership passes, and says so
- [X] T014 [US2] Inventory tests: owner passes, other seller 404, administrator passes, missing row after ownership (server/tests/Ecommerce.Inventory.Tests/SellerStockTests.cs)
- [X] T015 Run Inventory and Catalog suites

## Phase 4: The seller console

- [X] T016 [P] [US3] `Stock` service and `useStock` / `useSetStock` hooks in client/src/services/ and hooks/
- [X] T017 [US3] Quantity control on client/src/pages/shop-product/index.tsx, showing on hand and reserved
- [X] T018 [P] [US3] Strings in client/src/locales/vi/seller.json and en/seller.json
- [X] T019 [US3] Vitest tests: what it sends, the reserved figure shown, a refusal in the server's words
- [X] T020 [US3] Remove the "sellers cannot set stock yet" line from the create form's consequences

## Phase 5: End to end

- [X] T021 [US2] Two real sellers against the running stack: SC-002, 404 never 403
- [X] T022 [US1] SC-001: list, stock, buy - on-hand falls by exactly one, nothing left held
- [X] T023 `verify-saga.sh` and `verify-auth.sh`
- [X] T024 [US1] [US2] Bruno: a seller stocking their own, and a security-check for somebody else's

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

## Added in the 2026-09-27 backfill (work the pull request shows)

- [X] T031 [US1] `GetVariantOwnersAsync` and the `VariantOwnership` record on Catalog's `IProductRepository`, implemented in server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs as a three-column no-tracking projection
- [X] T032 [US2] `StockOwnership.RequireCanStockAsync` and `RoleNames` in server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/StockOwnership.cs - the one place that decides; an administrator returns before Catalog is asked
- [X] T033 [US2] `GrpcProductOwnership` bounded like Order's client (3 attempts, 5 s deadline, 200 ms / 1 s backoff) and a Catalog failure raised as `DependencyUnavailableException` (503), never 404
- [X] T034 [P] [US2] A fake `IProductOwnership` that counts calls, in server/tests/Ecommerce.Inventory.Tests/InventoryTestFixture.cs, so `An_administrator_does_not_cost_a_call_to_Catalog` can assert zero
- [X] T035 [P] [US2] `Catalog_being_unreachable_is_not_a_refusal` and `A_seller_cannot_stock_the_shops_own_product` in SellerStockTests.cs
- [X] T036 [US2] Bruno `seller/a customer cannot stock it.yml` - 403 at the door
- [X] T037 The design record completed to the specs/001 standard: data-model.md, quickstart.md, plan and research structure (2026-09-27)
- [X] T038 Merged as **#71** (`d94035a`) on 2026-09-23, closing #70: Catalog 115, Inventory 38, client 38 tests; Bruno 93/93 requests, 145/145 tests; `verify-saga.sh` and `verify-auth.sh` green

## Dependencies

Phase 1 → Phase 2 → Phase 3; Phase 4 needs only the unchanged endpoint and can run beside Phase 3;
Phase 5 needs all of them. T026 and T038 close the list.
