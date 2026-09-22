# Tasks: Product Images

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

## Phase 1: Setup

- [X] T001 Dockerfile: create `/app/data` owned by `$APP_UID` before `USER $APP_UID` in server/Dockerfile (research D7)
- [X] T002 [P] Compose: named volume `catalog_images` mounted at `/app/data` (not the subdirectory - research D7), `ProductImages__Root` set, in server/docker-compose.app.yml

## Phase 2: Foundational

- [X] T003 `Product.ImageContentType`, `Product.ImageUpdatedAt` in server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs
- [X] T004 Columns and both CHECK constraints in server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs; migration `AddProductImage`
- [X] T005 [P] `IProductImageStore` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs
- [X] T006 [P] `ImageFormat.Detect` (magic bytes) and `ProductImageKey` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/
- [X] T007 `FileSystemProductImageStore` with a startup writability check, registered in Infrastructure DependencyInjection.cs (research D8)
- [X] T008 `IProductRepository.TrySetImageAsync` as a guarded single-statement update, in the interface and ProductRepository.cs
- [X] T009 `ProductResponse.ImageUrl` and one mapping used by all three handlers that build a `ProductResponse`

## Phase 3: User Story 1 - upload (P1)

- [X] T010 [US1] `UploadProductImageCommand` + validator + handler: write, switch, delete (research D3), in Products/Images/UploadProductImage/
- [X] T011 [US1] `PUT /api/products/{id}/image`, Admin, `RequestSizeLimit`, in ProductsController.cs
- [X] T012 [US1] Tests in server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs: upload sets the row and the file; replace switches and deletes the old one; a lost race is 409 and leaves no file; unknown product is 404

## Phase 4: User Story 2 - shoppers see it (P1)

- [X] T013 [US2] `GetProductImageQuery` + `GET /api/products/{id}/image` with the cache headers (research D6)
- [X] T014 [P] [US2] Client: `imageUrl` on `Product`; `<img>` in CatalogPage.tsx and ProductPage.tsx, with the placeholder kept for null

## Phase 5: User Story 3 - bad uploads refused (P2)

- [X] T015 [US3] Tests: wrong type with an image label, oversized, empty; each leaves the row unchanged and no file behind; a store that fails on write leaves the row unchanged

## Phase 6: User Story 4 - remove (P3)

- [X] T016 [US4] `RemoveProductImageCommand` + `DELETE /api/products/{id}/image`; tests: remove clears row and file, removing twice is quiet

## Phase 7: Polish

- [X] T017 [P] Bruno: fixtures; `product/upload image`, `product/get image`; `security-checks/` wrong type 400, too big 400, customer upload 403
- [X] T018 Negative controls: header-trusting detection, unguarded switch, delete-before-write
- [X] T019 Verify through the gateway on the containerised stack, including after a container restart (the volume)
- [X] T020 [P] Docs: CLAUDE.md (service map, test count), the Catalog docs that describe product fields
- [ ] T021 PR `Closes #45`; CI green; squash-merge

## Dependencies

Phase 2 before everything. US1 before US2 (nothing to show) and US3 (same handler). US4 is independent
of US2/US3.

## What actually happened

- **Catalog.Tests 21/21** (8 + 13 in `ProductImageTests`), against real PostgreSQL with the real file
  store in a temp directory.
- Negative controls, each restored afterwards:
  - take the type from the label → 5 fail;
  - unguarded switch → the race test fails;
  - delete before write → 2 fail.
- **The startup check caught a real mistake.** The volume was first mounted on a path the image
  lacks, came up root-owned, and Catalog refused to start with the named directory (research D7).
- Through the gateway, with the Catalog container rebuilt:

  ```text
  upload pixel.png labelled text/plain -> imageUrl …/image?v=639256405524921980
  GET with v      -> 200 image/png, Cache-Control: public, max-age=31536000, immutable, nosniff, bytes identical
  GET without v   -> Cache-Control: no-cache
  3 MB / 50 MB    -> 400 "Request body too large" in 0.05 s   (planned 413 - corrected in the contract)
  restart catalog -> the image still serves 200 (the volume)
  ```

- **Bruno 62/62, 85 tests.**
- `npm run lint` shows no warnings and `npm run build` succeeds. **Not seen in a real browser.**
