---
description: "Task list for The Shop Is a Marketplace"
---

# Tasks: The Shop Is a Marketplace

> Written on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

**Input**: Design documents from `/specs/027-seller-accounts/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, one per cross-seller write, because SC-001 says "per operation, not once": the check
lives in many handlers and a single happy-path test would never find the one that forgot to call it.

No task list was written while building; this one is reconstructed from the merge diff, in the plan's
"Order of work". Every task below is in #64.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US4)

---

## Phase 1: Foundational - contracts and Identity

- [X] T001 [P] Add `SellerRegisteredEvent` and `SellerRenamedEvent` in `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/SellerEvents.cs`
- [X] T002 [P] Add `bool IsInRole(string role)` to `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/ICurrentUser.cs` and implement it in `CurrentUser.cs`; implement it in the test doubles in `server/tests/Ecommerce.{Cart,Catalog,Identity,Order}.Tests/*Fixture.cs`
- [X] T003 [P] Add `Seller` to `server/src/Services/Identity/Ecommerce.Identity.Domain/Constants/RoleNames.cs` so `DataInitializer` seeds it
- [X] T004 [P] Create `SellerProfile` in `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/SellerProfile.cs` and `SellerProfileConfiguration` (PK `UserId`, FK to `users` cascade, `ShopName` 100) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/SellerProfileConfiguration.cs`
- [X] T005 Add MassTransit, the EF outbox and `AddTransactionalOutboxEntities()` to Identity: `Ecommerce.Identity.Infrastructure.csproj`, `Ecommerce.Identity.WebApi.csproj`, `Persistence/ApplicationDbContext.cs` and `WebApi/Program.cs` (`UseBusOutbox()`, `IdentitySvc` endpoint prefix) - research D6
- [X] T006 Generate `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Migrations/20260922173126_AddSellerProfilesAndOutbox.cs`
- [X] T007 Add `AddSellerProfileAsync` and the profile reads to `Application/Common/Interfaces/IUserRepository.cs` and `Infrastructure/Persistence/Repositories/UserRepository.cs`

---

## Phase 2: User Story 1 - A seller lists what they sell (P1) 🎯 MVP

- [X] T008 [US1] Implement `RegisterSellerCommand`, validator and handler in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/RegisterSeller/RegisterSellerCommand.cs`: both roles, the profile, `SellerRegisteredEvent` staged and saved once
- [X] T009 [US1] Add `POST /api/auth/register-seller` to `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs`
- [X] T010 [P] [US1] Create the `Seller` read model in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Seller.cs` and `SellerConfiguration` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/SellerConfiguration.cs`; add `SellerId` to `Domain/Entities/Product.cs`; register the `DbSet` in `CatalogDbContext.cs`
- [X] T011 [US1] Generate `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260922173905_AddSellers.cs`
- [X] T012 [US1] Implement `ISellerRepository` and `SellerRepository` (guarded `TryRecordAsync`, batched `GetNamesAsync`) in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/ISellerRepository.cs` and `Infrastructure/Persistence/Repositories/SellerRepository.cs`; register it in `Infrastructure/DependencyInjection.cs`
- [X] T013 [US1] Implement `RecordSellerCommand` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Sellers/RecordSellerCommand.cs` and both consumers in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Consumers/SellerConsumers.cs`; register them in Catalog's `Program.cs`
- [X] T014 [US1] Take the seller from the token in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/CreateProduct/CreateProductCommandHandler.cs` (`SellerId` = caller when `Seller`, null for an administrator)
- [X] T015 [US1] Add `SellerId` and `SellerName` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs` and fill them with one lookup per page in `GetProductsQueryHandler.cs` and `GetProductByIdQueryHandler.cs`
- [X] T016 [P] [US1] Show `sellerName ?? t('product.theShop')` in `client/src/components/product/product-card/index.tsx` and `client/src/pages/product/index.tsx`; add `sellerId`/`sellerName` to `client/src/services/product/types.ts`

---

## Phase 3: User Story 2 - A seller may only touch their own listings (P1)

- [X] T017 [US2] Write `SellerOwnership.RequireCanWrite`/`CanWrite` (404 wording identical to a missing product; administrators pass) in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/SellerOwnership.cs`
- [X] T018 [US2] Call it from every product write: `DeleteProduct`, `AddProductVariant`, `UpdateProductVariant`, `SetProductTranslation`, `RemoveProductTranslation`, `SetOptionTranslation`, `SetVariantPrice`, `RemoveVariantPrice`, `UploadProductImage`, `RemoveProductImage` (under `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/`)
- [X] T019 [US2] Change the write actions in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs` from `Admin` to `Seller,Admin` - without this the ownership checks were unreachable and a seller got 403 on her own product
- [X] T020 [US2] Implement `GetMyProductsQuery` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Queries/GetMyProducts/GetMyProductsQuery.cs` and `GET /api/products/mine` (`Seller`)
- [X] T021 [P] [US2] Write `SellerOwnershipTests` in `server/tests/Ecommerce.Catalog.Tests/SellerOwnershipTests.cs` (16 tests; seller setup in `CatalogTestFixture.cs`)

---

## Phase 4: User Story 3 - The administrator moderates (P2)

- [X] T022 [US3] Administrators pass `SellerOwnership` and their own products get no seller (covered by T014, T017; tests `An_administrator_may_touch_anybodys_listing_because_that_is_moderation`, `An_administrators_product_belongs_to_the_shop_itself`)

---

## Phase 5: Renaming (FR-008)

- [X] T023 [US1] Implement `GetMyShopQuery` and `RenameShopCommand` (publish `SellerRenamedEvent`, save once) in `server/src/Services/Identity/Ecommerce.Identity.Application/Sellers/SellerCommands.cs`
- [X] T024 [US1] Add `SellersController` (`GET /api/sellers/me`, `PUT /api/sellers/me/shop-name`, `Seller`) in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/SellersController.cs`
- [X] T025 [US1] Add `sellers-route` and `sellers-root-route` to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` - found missing on the stack, where the rename was a 404

---

## Phase 6: User Story 4 and evidence

- [X] T026 [US4] Keep `products.SellerId` nullable with no backfill; confirm the seeded cameras read as the shop's own and still check out (`verify-saga.sh`, `verify-auth.sh`)
- [X] T027 [P] Add Bruno `bruno/seller/` (`register a seller`, `a seller lists a product`, `a seller cannot touch the shops product` asserting the exact 404, `the shop name reaches the catalogue`) and reorder the `security-checks` and `admin-audit` folders
- [X] T028 [P] Record the marketplace rules in `CLAUDE.md`
- [X] T029 Verify on the containerised stack: two sellers, 404 on every cross-write, `mine` 1 and 0, a customer 403 on create, a rename leaving the product's `UpdatedAt` unchanged (after letting the availability announcement settle)
- [X] T030 Verify Identity without a broker: registration 200 with RabbitMQ stopped, the outbox drains when it returns
- [X] T031 Run every test project (278 pass) and Bruno (91/91 requests, 138 tests)
- [X] T032 Complete this design record under `specs/027-seller-accounts/` to the specs/001 standard (2026-09-27)
- [X] T033 Merged as [#64](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/64) (`6938adf`) on 2026-09-22

---

## Dependencies & Execution Order

- Phase 1 before everything: the events, `IsInRole` and the outbox are used by every later phase.
- T010-T013 (the read model) before T015 (names on responses).
- T017 before T018; T018 and T019 together - either alone leaves sellers refused or unchecked.
- T025 before the rename can be exercised through the gateway.

## Notes

- 33 tasks. One test task (T021) holding 16 tests.
- No storefront sign-up task: registering a seller was API-only at this merge (see the plan's correction).
