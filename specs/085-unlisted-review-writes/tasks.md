---
description: "Task list for No reviews off the shelf"
---

# Tasks: No reviews off the shelf

> Completed on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Input**: Design documents from `/specs/085-unlisted-review-writes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included - who may write is an authorization boundary.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 no write off the shelf, US2 the page says not eligible, US3 back on sale

The original three tasks are kept in full: original T001 is T001, T002 is T002-T003, T003 is T004-T006.

---

## Phase 1: Test first

- [X] T001 [US1] [US2] [US3] A test in `server/tests/Ecommerce.Catalog.Tests/UnlistedProductReadsTests.cs` (`Off_the_shelf_nobody_writes_a_review_and_its_rating_does_not_move`, with a `RatingAsync` helper):
  - an eligible customer reviews a product on sale;
  - it is taken down, and a second review is a 404 while the page says not eligible;
  - the rating stays;
  - back on sale, a review is written again.

## Phase 2: Implementation

- [X] T002 [US1] [US3] `ReviewFeatures`: `WriteReviewCommand` asks `OnSale` (`IsListed && IsActive`) and throws `Product not found.` before the own-product and eligibility checks, in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs`
- [X] T003 [US2] `GetMyReviewQuery` asks `OnSale` too, in the same file

## Phase 3: Verification and docs

- [X] T004 Two mutations (one gate removed at a time), each turning T001 red
- [X] T005 [P] Bruno `bruno/seller/a review of it is a 404.yml` (seq 82) against the rebuilt Catalog, and the docs: `docs/features/ratings-and-reviews.md`, `CLAUDE.md`, counts in `docs/overview/project-overview.md` and `docs/testing/testing-strategy.md`, `docs/project/timeline.md` and `docs/project/backlog.md`
- [X] T006 Merged as #177 on 2026-09-26 UTC (closes #174), after Catalog 206/206 and Bruno 268/268 requests and 438/438 tests

---

## Dependencies & Execution Order

T001 (red) → T002 → T003 → T004-T006. T002 and T003 are one file.
