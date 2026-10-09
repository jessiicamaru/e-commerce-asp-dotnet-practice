# Implementation Plan: Related products and recently viewed

**Branch**: `feature/375-related-and-recent` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md) | **Issue**: #375

## Summary

A related-products read built on the listing (category, then department, most reviewed first), an `ids` filter and a
`rating_desc` sort on the listing, and two storefront rows: related products under a product, and the browser's recently
viewed products under a product and on the landing page.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: MediatR, FluentValidation, EF Core; TanStack Query
**Storage**: none new on the server; `localStorage` in the browser
**Testing**: `RelatedProductsTests` (PostgreSQL); Vitest; Bruno; a browser check
**Constraints**: anonymous and cacheable; nothing about an off-shelf product leaks
**Scale/Scope**: one query + handler, one filter, one sort, a controller action; a browser store, a hook, two rows

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own data. |
| **II. Clean Architecture Layering** | **Pass.** The query in Application over the repository's listing; the filter and sort in Infrastructure; thin controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not applicable.** Reads only. |
| **IV. Identity Comes From the Token** | **Pass.** Anonymous reads; no user in any request; the browser's history never leaves it. |
| **V. Evidence Over Assumption** | **Pass.** Ranking, fill and exclusions tested on PostgreSQL; the rows in a browser. |
| **Schema compatibility** | **Pass.** No migration. |

**Post-design re-check**: unchanged - see `tasks.md`.

## Project Structure

```text
specs/163-related-and-recent/
server/src/Services/Catalog/
  Ecommerce.Catalog.Application/Products/Queries/GetRelatedProducts/GetRelatedProductsQuery.cs
  Ecommerce.Catalog.Application/Products/Queries/GetProducts/*      (ids)
  Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs (ProductFilter.Ids)
  Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs (ids, rating_desc)
  Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs
server/tests/Ecommerce.Catalog.Tests/RelatedProductsTests.cs
client/packages/core: services/product, hooks/product, utils/product/recently-viewed, locales
client/apps/storefront: components/product/product-row, pages/product, pages/catalog
bruno/product/
```

## Complexity Tracking

None.
