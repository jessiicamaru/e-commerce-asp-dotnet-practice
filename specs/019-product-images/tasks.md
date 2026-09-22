# Tasks: Product Images

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

## Phase 1: Setup

- [ ] T001 Dockerfile: create `/app/data` owned by `$APP_UID` before `USER $APP_UID` in server/Dockerfile (research D7)
- [ ] T002 [P] Compose: named volume `catalog_images` mounted at `/app/data/product-images`, `ProductImages__Root` set, in server/docker-compose.app.yml

## Phase 2: Foundational

- [ ] T003 `Product.ImageContentType`, `Product.ImageUpdatedAt` in server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs
- [ ] T004 Columns and both CHECK constraints in server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs; migration `AddProductImage`
- [ ] T005 [P] `IProductImageStore` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs
- [ ] T006 [P] `ImageFormat.Detect` (magic bytes) and `ProductImageKey` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/
- [ ] T007 `FileSystemProductImageStore` with a startup writability check, registered in Infrastructure DependencyInjection.cs (research D8)
- [ ] T008 `IProductRepository.TrySetImageAsync` as a guarded single-statement update, in the interface and ProductRepository.cs
- [ ] T009 `ProductResponse.ImageUrl` and one mapping used by all three handlers that build a `ProductResponse`

## Phase 3: User Story 1 - upload (P1)

- [ ] T010 [US1] `UploadProductImageCommand` + validator + handler: write, switch, delete (research D3), in Products/Images/UploadProductImage/
- [ ] T011 [US1] `PUT /api/products/{id}/image`, Admin, `RequestSizeLimit`, in ProductsController.cs
- [ ] T012 [US1] Tests in server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs: upload sets the row and the file; replace switches and deletes the old one; a lost race is 409 and leaves no file; unknown product is 404

## Phase 4: User Story 2 - shoppers see it (P1)

- [ ] T013 [US2] `GetProductImageQuery` + `GET /api/products/{id}/image` with the cache headers (research D6)
- [ ] T014 [P] [US2] Client: `imageUrl` on `Product`; `<img>` in CatalogPage.tsx and ProductPage.tsx, with the placeholder kept for null

## Phase 5: User Story 3 - bad uploads refused (P2)

- [ ] T015 [US3] Tests: wrong type with an image label, oversized, empty; each leaves the row unchanged and no file behind; a store that fails on write leaves the row unchanged

## Phase 6: User Story 4 - remove (P3)

- [ ] T016 [US4] `RemoveProductImageCommand` + `DELETE /api/products/{id}/image`; tests: remove clears row and file, removing twice is quiet

## Phase 7: Polish

- [ ] T017 [P] Bruno: fixtures; `product/upload image`, `product/get image`; `security-checks/` wrong type 400, too big 400, customer upload 403
- [ ] T018 Negative controls: header-trusting detection, unguarded switch, delete-before-write
- [ ] T019 Verify through the gateway on the containerised stack, including after a container restart (the volume)
- [ ] T020 [P] Docs: CLAUDE.md (service map, test count), the Catalog docs that describe product fields
- [ ] T021 PR `Closes #45`; CI green; squash-merge

## Dependencies

Phase 2 before everything. US1 before US2 (nothing to show) and US3 (same handler). US4 is independent
of US2/US3.
