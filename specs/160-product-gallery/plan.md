# Implementation Plan: A gallery of photographs per product

**Branch**: `feature/368-product-gallery` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md) | **Issue**: #368

## Summary

A new Catalog table, `product_photos`, for the photographs after the cover; the cover stays on `products` (research
D1). Four write endpoints (add, remove, make cover, reorder) and one read, each following specs/019's write-switch-delete
order; the lookup and the review queue carry `photos`. A gallery on the product page, a photographs card on the seller's
page, the photographs in the back office's review row.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core (migration), MediatR, FluentValidation; TanStack Query; shadcn/ui
**Storage**: 1 new table in `ecommerce_catalog_db`; the image store (S3 / directory) gains the `photo-` key form
**Testing**: `ProductGalleryTests` (PostgreSQL + the image store), `MyDataTests` (inventory), `CatalogueWritesTests`
(eviction), `ProductImageStoreTests` (key pattern); Vitest; Bruno; a browser check
**Constraints**: expand-only; at every moment every row names a file that exists; 10 photographs at most
**Scale/Scope**: entity + configuration + migration, 5 commands/queries, repository methods, controller actions,
3 client screens

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own table and image store; no other service reads or is told. |
| **II. Clean Architecture Layering** | **Pass.** Entity in Domain, rules and handlers in Application, mapping, migration and the transaction in Infrastructure, thin controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each change's rows, its audit entry and a review resubmission commit together (the cover switch joins them in one transaction, research D3); files are written before and deleted after, so a failure leaves at worst an orphan. No new message. |
| **IV. Identity Comes From the Token** | **Pass.** `SellerOwnership` from `ICurrentUser`; staff by role. |
| **V. Evidence Over Assumption** | **Pass.** Every operation tested on PostgreSQL with a real store; the review rule by mutation; the gallery in a browser. |
| **Schema compatibility** | **Pass.** One new table, nothing dropped, renamed or narrowed; an older image shows the cover and ignores the rest. |

**Post-design re-check**: unchanged - see `tasks.md`.

## Project Structure

```text
specs/160-product-gallery/            spec, plan, research, data-model, contracts, quickstart, checklists, tasks

server/src/Services/Catalog/
  Ecommerce.Catalog.Domain/Entities/ProductPhoto.cs
  Ecommerce.Catalog.Application/Products/Images/ProductGallery.cs        (commands, query, rules)
  Ecommerce.Catalog.Application/Products/Images/ProductImageKey.cs       (+ photo key and address)
  Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs  (+ gallery methods)
  Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs       (+ Photos)
  Ecommerce.Catalog.Infrastructure/Persistence/Configurations/ProductPhotoConfiguration.cs
  Ecommerce.Catalog.Infrastructure/Persistence/Migrations/*_AddProductPhotos.cs
  Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs
  Ecommerce.Catalog.Infrastructure/Images/ProductImageKeys.cs            (key pattern)
  Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs
server/tests/Ecommerce.Catalog.Tests/ProductGalleryTests.cs

client/packages/core/src/services/product, hooks/product, locales/*/{catalog,seller,admin}.json
client/apps/storefront/src/components/product/product-gallery
client/apps/storefront/src/components/seller/product-photos
client/apps/back-office/src/pages/admin-products
bruno/product-photos/
```

## Complexity Tracking

None. The one non-obvious piece - the cover change copying bytes - is the specs/019 order applied once more, and is
justified in research D3 against moving the cover into the table.
