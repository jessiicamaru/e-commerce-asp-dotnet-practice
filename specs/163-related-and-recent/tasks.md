---
description: "Task list for Related products and recently viewed"
---

# Tasks: Related products and recently viewed

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 `rating_desc` sort and the `ids` filter on the listing
- [x] T002 `GET /api/products/{id}/related` - category, then department, never itself, empty when off the shelf; cached
- [x] T003 `RelatedProductsTests` on PostgreSQL; a mutation

## Client

- [x] T004 Service and hooks; the browser's recently viewed list
- [x] T005 A row of cards; related and recently viewed on the product page; recently viewed on the landing page
- [x] T006 Client tests; locales

## Contract, docs

- [x] T007 Bruno for both reads and the refusals; `docs/reference` regenerated
- [x] T008 On the stack: the quickstart and a browser check
- [x] T009 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T010 Merged, closes #375

## Evidence

Verified on 2026-10-09 against the compose stack with Catalog and the storefront rebuilt from this branch:

- **Server tests**: `Ecommerce.Catalog.Tests` 351 passed, 0 failed, on PostgreSQL - 7 in `RelatedProductsTests` (the
  category first and most reviewed first, then the department; never the product, off-shelf or another department; a
  full category needs no department; unknown and off-shelf both empty; limits and 24 ids refused; the listing by ids).
- **Mutations**: without the department fill, and ranked by name instead of reviews, a test fails each time. Restored.
- **Client**: 147 files, 813 tests passed - the browser's list (most recent first, each once, 12, unreadable data,
  blocked storage, subscribers), the product page's rows (related asked for this product, the view remembered, recently
  viewed in the browser's order without this product, no heading for an empty row). Returning the listing's order
  instead of the browser's fails a test. Lint clean; both apps build.
- **Quickstart**: the Sony A7 IV's related products are 8 other mirrorless cameras (none of the seeded products has a
  review, so the name breaks the tie).
- **In a browser**: the A7 IV page shows "Related products" with 8 cards; opening the Canon EOS R50 and the Fujifilm X-T5
  from that row, the X-T5 page's "Recently viewed" reads R50, A7 IV (not the X-T5), and the landing page's reads X-T5,
  R50, A7 IV. The browser's history was cleared afterwards. (The pane was not drawing, so this was read from the page,
  not from screenshots.)
- **Bruno**: 4 new requests at the end of `product/` (seq 90-93); the whole collection 462/462 requests, 739/739 tests.
- **Found on the way**: the unit tests' network guard stubs `fetch`, but the app's `http` is axios over
  XMLHttpRequest - an unmocked service call errors quietly inside a query instead of failing the test. Filed as a
  separate task rather than changed here.
- **Docs**: `docs/reference` regenerated (227 endpoints).
