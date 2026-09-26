---
description: "Task list for A product inserted without a review status waits for review"
---

# Tasks: A product inserted without a review status waits for review

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md)

**Tests**: included, written first - the new test failed before the migration.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Establish

- [X] T001 Read the mapping, the enum and the snapshot: the model declares no default, the enum's CLR default is `Approved` (research D2)

## Phase 2: Tests first

- [X] T002 [US1] `A_product_inserted_without_a_review_status_waits_for_review` in `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs` - red before
- [X] T003 [US2] `The_shops_own_product_is_on_sale_as_listed` reads the stored status too

## Phase 3: The migration (US1)

- [X] T004 [US1] `dotnet ef migrations add ReviewStatusDefaultsToPending` (Catalog); `Up`/`Down` by SQL; snapshot unchanged
- [X] T005 [P] [US2] A comment on `ProductConfiguration`'s `ReviewStatus` mapping: no `HasDefaultValue`, and why

## Phase 4: Verification and docs

- [X] T006 Mutations (quickstart Scenario 3); `Ecommerce.Catalog.Tests` 217/217
- [X] T007 Docs: `docs/features/catalog.md`, CLAUDE.md, `docs/project/backlog.md`, `docs/project/timeline.md` (`generate_reference.py` run: it records no defaults, only its stamps changed - not committed)
- [X] T008 Merged as #192 (2026-09-27), closing #184

## Verification

- Before: `A_product_inserted_without_a_review_status_waits_for_review` red (stored `Approved`).
- After: `Ecommerce.Catalog.Tests` 217/217.
- Mutations, each restored: the migration setting `'Approved'` - the new test red; a model-level
  `HasDefaultValue(Pending)` - every test red at fixture start (EF refuses to migrate an unrecorded model change).
