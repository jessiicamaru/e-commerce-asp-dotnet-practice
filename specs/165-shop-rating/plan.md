# Implementation Plan: A shop's rating

**Branch**: `feature/379-shop-rating` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md) | **Issue**: #379

## Summary

The shop's average rating and review count on `GET /api/shops/{id}`, computed by the same code as the seller's insights;
shown under the shop's name on its page and beside "Sold by" on a seller's product page.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: MediatR, EF Core; TanStack Query
**Storage**: none new - read from `products.RatingAverage` / `RatingCount`
**Testing**: `ShopRatingTests` (PostgreSQL); Vitest; Bruno; a browser check
**Constraints**: anonymous and cacheable; the shop page and the insights agree
**Scale/Scope**: one repository method shared by two reads, two response fields, two places in the storefront

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own data: its products and their reviews. |
| **II. Clean Architecture Layering** | **Pass.** The aggregate in Infrastructure behind `IProductRepository`; the handler in Application; the controller unchanged. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not applicable.** A read; nothing is written or published. |
| **IV. Identity Comes From the Token** | **Pass.** An anonymous read by shop id; the insights keep reading the seller from the token. |
| **V. Evidence Over Assumption** | **Pass.** Weighting, hidden reviews, off-shelf products and agreement with the insights tested on PostgreSQL; both pages in a browser. |
| **Schema compatibility** | **Pass.** No migration. |

**Post-design re-check**: unchanged - see `tasks.md`.

## Project Structure

```text
specs/165-shop-rating/
server/src/Services/Catalog/
  Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs   (RatingOfSellerAsync, ShopRating)
  Ecommerce.Catalog.Application/Sellers/ShopFeatures.cs                   (ShopResponse + rating)
  Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs
  Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductViewRepository.cs (calls the shared method)
server/tests/Ecommerce.Catalog.Tests/ShopRatingTests.cs
client/packages/core: services/shops/types, hooks (useShopFront), locales
client/apps/storefront: pages/shop-front, pages/product
bruno/shops/
```

## Complexity Tracking

None.
