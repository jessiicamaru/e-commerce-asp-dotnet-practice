---
description: "Task list for A gallery of photographs per product"
---

# Tasks: A gallery of photographs per product

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 `ProductPhoto` entity, configuration and the `AddProductPhotos` migration (expand-only)
- [ ] T002 Keys and addresses: `photo-` keys, the store's key pattern, the live keys of the orphan report
- [ ] T003 Add (cover when none), remove (the cover promotes the first), make cover, reorder - write, switch, delete;
  ownership, review, audit (research D3-D8)
- [ ] T004 Serve a photograph (cache headers, the access key off the shelf); `photos` on the lookup and the review queue
- [ ] T005 Deleting a product deletes its photographs' files; `CatalogueWrites` and the personal-data inventory know the table
- [ ] T006 `ProductGalleryTests` on PostgreSQL with a real store; a mutation shown failing

## Client

- [ ] T007 Service, hooks, types; locale strings (incl. audit labels)
- [ ] T008 Storefront: the gallery on the product page (thumbnails, variant photograph, phone scroll)
- [ ] T009 Seller: the photographs card (add several, remove, move, make cover)
- [ ] T010 Back office: the photographs in the review row
- [ ] T011 Client tests

## Contract, docs

- [ ] T012 Bruno for every endpoint and refusal; `docs/reference` regenerated
- [ ] T013 On the stack: the quickstart, the gallery in a browser
- [ ] T014 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T015 Merged, closes #368

## Evidence

(Filled in when the work is verified.)
