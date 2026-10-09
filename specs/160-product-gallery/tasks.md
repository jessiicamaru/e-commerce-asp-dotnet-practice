---
description: "Task list for A gallery of photographs per product"
---

# Tasks: A gallery of photographs per product

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 `ProductPhoto` entity, configuration and the `AddProductPhotos` migration (expand-only)
- [x] T002 Keys and addresses: `photo-` keys, the store's key pattern, the live keys of the orphan report
- [x] T003 Add (cover when none), remove (the cover promotes the first), make cover, reorder - write, switch, delete;
  ownership, review, audit (research D3-D8)
- [x] T004 Serve a photograph (cache headers, the access key off the shelf); `photos` on the lookup and the review queue
- [x] T005 Deleting a product deletes its photographs' files; `CatalogueWrites` and the personal-data inventory know the table
- [x] T006 `ProductGalleryTests` on PostgreSQL with a real store; a mutation shown failing

## Client

- [x] T007 Service, hooks, types; locale strings (incl. audit labels)
- [x] T008 Storefront: the gallery on the product page (thumbnails, variant photograph, phone scroll)
- [x] T009 Seller: the photographs card (add several, remove, move, make cover)
- [x] T010 Back office: the photographs in the review row
- [x] T011 Client tests

## Contract, docs

- [x] T012 Bruno for every endpoint and refusal; `docs/reference` regenerated
- [x] T013 On the stack: the quickstart, the gallery in a browser
- [x] T014 Docs: catalog page, CLAUDE.md, timeline, backlog
- [x] T015 Merged in #371, closes #368

## Evidence

Verified on 2026-10-09 against the compose stack rebuilt from this branch:

- **Server tests**: `Ecommerce.Catalog.Tests` 330 passed, 0 failed, on PostgreSQL and the filesystem store - 11 in
  `ProductGalleryTests`, and `product_photos` added to `CatalogueWritesTests`' eviction cases. The filesystem store
  refuses a key outside its pattern, so the gallery tests passing is also the `photo-` key form accepted.
- **Mutations**: without `ProductReview.AfterSellerEditAsync` on add, the review test fails; without the promotion in
  `RemoveProductImageCommandHandler`, the cover-removal test fails. Both restored.
- **Client**: 143 files, 785 tests passed (the gallery 4, the seller's card 6, the review row 1); without the room
  check the seller's card sends more than 10 and its test fails. Lint clean but for an older warning in the
  specifications panel; both apps build.
- **Migration**: `AddProductPhotos` applied at the container's start; one new table, nothing else touched.
- **Bruno**: the whole collection, 709 of 710 tests; the one failure is `my-data/payment`, filed as #364. The 11 new
  requests at the end of `product/` (seq 71-81) pass: cover, two photographs, a photograph served as `image/png` with
  `nosniff`, the lookup in order, reorder, a partial reorder 400, make cover, a customer 403, remove 204, again 404.
- **In a browser**: a product given a cover and two photographs (plain PNGs made locally - the seed has no honest
  photographs) shows the cover large and 3 thumbnails; choosing the third shows it; at phone width the strip sits under
  the photograph. The first look found the selected thumbnail's ring clipped by the strip's overflow - padded. The
  photographs were removed afterwards: no cover, no photographs, still Approved (staff changes do not resubmit), and the
  orphan report empty.
- **Not checked in a browser**: the seller's photographs card (it needs a seller account with a confirmed address);
  its behaviour is covered by its unit tests.
- **Docs**: `docs/reference` regenerated (222 endpoints, 65 tables).
