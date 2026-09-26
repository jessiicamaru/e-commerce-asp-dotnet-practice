# Implementation Plan: One rule for "off the shelf"

**Branch**: `092-one-shelf-rule` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #185

## Summary

Add `Product.OnShelf => IsListed && IsActive` to the Domain and route every decision about "on the shelf" through
it: the public lookup and what hangs on it (`MaySee`, image access and cacheability), the listing (spelled out in SQL),
views, reviews, questions, saving, saved-product notices, `CatalogPricing` and `ProductVariant.Sellable`. A withdrawn
but approved product becomes the same 404 as a taken-down one. No schema or contract change.

## Technical Context

- `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs`, `ProductVariant.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs`,
  `.../Persistence/Repositories/ProductRepository.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.Application/`: `Common/ProductReview.cs`,
  `Products/Images/ProductImageKey.cs`, `GetProductImage/GetProductImageQuery.cs`, `GetVariantImageQuery.cs`,
  `Products/Views/ProductViewFeatures.cs`, `Reviews/ReviewFeatures.cs`, `Questions/QuestionFeatures.cs`,
  `Products/Saved/SavedProductFeatures.cs`, `SavedProductNotices.cs`, `Products/Availability/RecordStockAvailabilityCommandHandler.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/CatalogPricingService.cs`
- `server/tests/Ecommerce.Catalog.Tests/UnlistedProductReadsTests.cs`

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core 10 + Npgsql, MediatR 12.4.1

**Storage**: PostgreSQL `ecommerce_catalog_db` (5433) - no schema change

**Testing**: xUnit against a real PostgreSQL and SeaweedFS (`Ecommerce.Catalog.Tests`); mutation checks

**Target Platform**: Catalog (5057, gRPC 6057)

**Performance Goals**: none - one more boolean in a filter that already reads the row

**Constraints**: the seller's and staff's view unchanged; no route may start selling or stop selling anything that is
on the shelf today

**Scale/Scope**: one property, fourteen call sites, one test

## Design

- Domain: `OnShelf` beside `IsListed`, documented as the single rule; `ProductVariant.Sellable` uses it.
- EF: `builder.Ignore(p => p.OnShelf)`.
- SQL listing: `ReviewStatus == Approved && IsActive`, commented as `OnShelf`.
- Every in-memory check replaced by `OnShelf` (or `OnShelf && Availability` where stock matters).

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own rule over its own rows; Order learns sellability from `CatalogPricing` as before. |
| **II. Clean Architecture Layering** | **Pass.** The rule moves into the Domain entity, where the Application, Infrastructure and WebApi layers all may read it; nothing depends upward. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable** - no write, message or transaction changes. |
| **IV. Identity Comes From the Token** | **Pass.** `MaySee`'s seller and staff exceptions still read `ICurrentUser`; nothing from the request. |
| **V. Evidence Over Assumption** | **Pass.** The call sites and the absence of any writer of `Product.IsActive` were surveyed (research D2); the new test failed before the fix; three mutations each caught; `Ecommerce.Catalog.Tests` 216/216. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/092-one-shelf-rule/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D3
├── data-model.md        # The derived properties and the states
├── quickstart.md
├── contracts/
│   └── http-api.md      # The answers that change for a withdrawn product
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As listed in Technical Context, plus `CLAUDE.md`, `docs/features/catalog.md`, `docs/features/admin-insights.md`,
`docs/project/backlog.md`, `docs/project/timeline.md`.

No endpoint, message, table or gateway route changes, so `docs/reference/` is not regenerated.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- There is still no command to withdraw or restore a product; this makes the state behave consistently when it exists.
