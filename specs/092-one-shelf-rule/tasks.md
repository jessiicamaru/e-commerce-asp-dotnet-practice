---
description: "Task list for One rule for off the shelf"
---

# Tasks: One rule for "off the shelf"

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the new test failed before the fix.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Survey

- [X] T001 Every reader of `IsListed` / `IsActive` on a product, and every writer of `Product.IsActive` (research D2)

## Phase 2: Test first (US1)

- [X] T002 [US1] `A_withdrawn_product_is_off_the_shelf_for_reads_as_it_is_for_writes` in `server/tests/Ecommerce.Catalog.Tests/UnlistedProductReadsTests.cs` - red before the fix

## Phase 3: One definition (US1, US2)

- [X] T003 [US2] `Product.OnShelf`; `ProductVariant.Sellable` through it; `Ignore(p => p.OnShelf)`
- [X] T004 [US1] `MaySee`, `MayServe`, both image queries' public flag, views, reviews, questions, saving, the saved list, `SavedProductNotices`, `RecordStockAvailability`, `CatalogPricing` (x2) use it
- [X] T005 [US1] The listing's SQL filter: `ReviewStatus == Approved && IsActive`

## Phase 4: Verification and docs

- [X] T006 Mutations (quickstart Scenario 4) - each red; `Ecommerce.Catalog.Tests` 216/216; `IsListed` read only by `OnShelf`
- [X] T007 Docs: `docs/features/catalog.md`, `docs/features/admin-insights.md`, `docs/project/backlog.md`, `docs/project/timeline.md`, CLAUDE.md
- [X] T008 Merged as #191 (2026-09-27), closing #185

## Verification

- Before: `A_withdrawn_product_is_off_the_shelf_for_reads_as_it_is_for_writes` red (the lookup answered).
- After: `Ecommerce.Catalog.Tests` 216/216.
- Mutations, each restored: `OnShelf => IsListed` - red; the listing without `IsActive` - red; `MaySee` on `IsListed` - red.
