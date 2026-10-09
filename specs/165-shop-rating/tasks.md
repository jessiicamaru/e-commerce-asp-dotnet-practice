---
description: "Task list for A shop's rating"
---

# Tasks: A shop's rating

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 `IProductRepository.RatingOfSellerAsync`, called by the shop page and the seller's insights
- [x] T002 `ShopResponse.RatingAverage` / `RatingCount`
- [x] T003 `ShopRatingTests` on PostgreSQL; a mutation

## Client

- [x] T004 The types; the shop page's rating or "no reviews yet"
- [x] T005 The product page: the shop's rating beside "Sold by", for a seller's product
- [x] T006 Client tests; locales

## Contract, docs

- [x] T007 Bruno checks the fields; `docs/reference` regenerated
- [x] T008 On the stack: the quickstart and a browser check
- [x] T009 Docs: catalog page, CLAUDE.md, timeline, backlog
- [x] T010 Merged in #380, closes #379

## Evidence

Verified on 2026-10-09 against the compose stack with Catalog and the storefront rebuilt from this branch (merged with
main after #378):

- **Server tests**: `Ecommerce.Catalog.Tests` 363 passed, 0 failed, on PostgreSQL - 4 in `ShopRatingTests`, with real
  reviews written and hidden through the commands: weighted by each product's reviews (4 once and 2 three times is 2.5
  over 4) and equal to the seller's insights; no review is null and another shop's reviews are not counted; hidden out,
  restored back; a product off the shelf still counts. `SellerProductInsightsTests` unchanged and passing.
- **Mutation**: counting only active products fails `A_product_off_the_shelf_still_counts`. Restored.
- **Client**: 149 files, 824 tests passed - the shop page's rating and "no reviews yet", the product page's rating
  beside "Sold by" (asked of `s1`; the shop's own product asks nothing; none shown without reviews), and the review hook
  re-reading the shops' pages (dropping the invalidation fails it). Three apps type-check; lint clean.
- **Found on the way**: the product page caches the shop's page, so writing a review now re-reads `shop-front` too -
  otherwise "Sold by" kept the number from before the review until the cache went stale.
- **Quickstart**: `GET /api/shops/{id}` of a shop with nothing reviewed answers `ratingAverage: null`,
  `ratingCount: 0`. No shop on the local stack has a reviewed product, so the rating with reviews was checked by the
  browser flow below.
- **In a browser** (Playwright in Edge, `e2e/flows.spec.ts`, all 6 passed): after the flow's customer receives and
  reviews the camera with 5 stars, signed out, the shop page shows "Shop rated 5.0 out of 5" and "1 review", and the
  camera's page shows the same stars and "5.0 (1)" beside "Sold by". This step stays in CI's browser job.
- **Bruno**: `seller/the shop page is public` checks the two fields and that they equal the seller's insights; the whole
  collection 464/464 requests, 743/743 tests.
- **Docs**: `docs/reference` regenerated (228 endpoints; no new route).
