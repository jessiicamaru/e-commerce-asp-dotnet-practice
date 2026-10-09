---
description: "Task list for Related products and recently viewed"
---

# Tasks: Related products and recently viewed

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 `rating_desc` sort and the `ids` filter on the listing
- [ ] T002 `GET /api/products/{id}/related` - category, then department, never itself, empty when off the shelf; cached
- [ ] T003 `RelatedProductsTests` on PostgreSQL; a mutation

## Client

- [ ] T004 Service and hooks; the browser's recently viewed list
- [ ] T005 A row of cards; related and recently viewed on the product page; recently viewed on the landing page
- [ ] T006 Client tests; locales

## Contract, docs

- [ ] T007 Bruno for both reads and the refusals; `docs/reference` regenerated
- [ ] T008 On the stack: the quickstart and a browser check
- [ ] T009 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T010 Merged, closes #375

## Evidence

(Filled in when the work is verified.)
