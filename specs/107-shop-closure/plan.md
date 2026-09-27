# Implementation Plan: A seller pauses their shop, and staff close one

**Branch**: `107-shop-closure` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #214

## Summary

Catalog's `sellers` row gains a seller's pause and a staff closure with a reason. The existing product flag
`SellerSuspended` is widened to mean "the shop is not open" and recomputed from all three reasons by one statement.
Every change of state calls that statement, including the ban and the approval of a product. The seller pauses and
reopens from `/shop`; staff close from the shop's page and reopen from `/admin/shops`.

## Technical Context

- **Catalog**:
  - `Seller` columns and migration `AddShopPauseAndClosure`.
  - `SellerRepository.ApplyShopStateAsync`, and the moves `TryPauseAsync`, `TryResumeAsync`, `TryCloseAsync` and
    `TryReopenAsync`.
  - `TryRecordSuspensionAsync` uses the shared statement; `ProductRepository.TryReviewAsync` sets the flag on approval.
  - `Sellers/ShopClosure.cs` holds the commands, queries, validator and handlers.
  - `ShopsController` gets six routes; `GetShopQuery` gains `Paused`.
- **Shared**: `notification-kinds.json` gains `ShopClosed` and `ShopReopened`.
- **Storefront**:
  - `services/shop` (state, pause, reopen, close, reopen as staff, closed list).
  - `hooks/shop`.
  - The `ShopStateCard` on `/shop`, the banner and the staff dialog on `/shops/:id`, and the tab on `/admin/shops`.
  - Words and notice wording.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, MassTransit outbox (audit, notices); TanStack Query

**Storage**: 4 columns in `ecommerce_catalog_db`

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Catalog (5057), the storefront

**Performance Goals**: One `UPDATE` over a seller's products per change. That is the same cost as a ban today.

**Constraints**: No new status value; one shelf rule; a seller never reopens a staff closure.

**Scale/Scope**: 4 columns, 6 routes, 2 notice kinds, 3 pages touched

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog decides, with no new call to or from another service. Order is untouched: paid orders still ship. |
| **II. Clean Architecture Layering** | **Pass.** The repository interface is in Application, and the SQL is in Infrastructure. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each move is one guarded `UPDATE`. The products' flag, the audit entry and the notices are written in the same transaction through the outbox. A retry after success answers 409 and writes nothing. |
| **IV. Identity Comes From the Token** | **Pass.** The seller's routes take no seller id (`mine`). Staff routes name the shop, and the role is the permission. |
| **V. Evidence Over Assumption** | **Planned.** Catalog tests against PostgreSQL for each pair of reasons; mutations; Bruno through rebuilt containers. Recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/107-shop-closure/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/Seller.cs
├── Ecommerce.Catalog.Application/Sellers/ShopClosure.cs          (new)
├── Ecommerce.Catalog.Application/Sellers/ShopFeatures.cs
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/SellerRepository.cs
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs
├── Ecommerce.Catalog.Infrastructure/Migrations/*_AddShopPauseAndClosure.cs
└── Ecommerce.Catalog.WebApi/Controllers/ShopsController.cs
server/tests/Ecommerce.Catalog.Tests/ShopClosureTests.cs            (new)
client/src/components/seller/shop-state/                           (new)
client/src/pages/shop, shop-front, admin-shops
bruno/seller/, bruno/security-checks/
```

## Complexity Tracking

No violation to justify. The widened meaning of `products.SellerSuspended` is recorded in research D2 with the
alternatives it beat. The column keeps its name, so a rolled-back image still reads it.
