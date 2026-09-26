---
description: "Task list for A Storefront That Looks Like a Shop"
---

# Tasks: A Storefront That Looks Like a Shop

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Input**: Design documents from `/specs/025-storefront-redesign/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: `DeleteCategoryTests` for the endpoint. The client had no unit test runner at this merge, so
the visual tasks were verified by reading screenshots ([research.md D6](./research.md)).

Reconstructed from the merge diff; every task below is in #62.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US5)

---

## Phase 1: Foundational - theme

- [X] T001 Rewrite the light and `.dark` theme tokens in `client/src/index.css`: warm background, white cards, lime `--primary` with dark foreground, `--radius: 1rem`
- [X] T002 Tint the page and widen it to `max-w-6xl` in `client/src/layouts/main-layout/index.tsx`

---

## Phase 2: User Story 1 - A landing that reads as a shop (P1) 🎯 MVP

- [X] T003 [US1] Create `CatalogHero` in `client/src/components/catalog/catalog-hero/index.tsx`: eyebrow, heading, browse link, product count, six category chips, featured product inside the panel
- [X] T004 [US1] Show the hero only on the landing view and feature the dearest product in `client/src/pages/catalog/index.tsx`; widen grid cards to `minmax(15rem,1fr)`
- [X] T005 [P] [US1] Rework `client/src/components/product/product-card/index.tsx`: the whole card is the link, an out-of-stock badge on the picture, a "Sold by" line
- [X] T006 [P] [US1] Add the `hero.*`, `product.soldBy` and `product.theShop` strings to `client/src/locales/en/catalog.json` and `client/src/locales/vi/catalog.json`

---

## Phase 3: User Story 2 - Search from any page (P2)

- [X] T007 [US2] Add the search form, rounded nav links and cart line count to `client/src/components/layout/top-bar/index.tsx`, navigating to `/?q=`; request the cart only when signed in
- [X] T008 [P] [US2] Add `searchPlaceholder` to `client/src/locales/en/common.json` and `client/src/locales/vi/common.json`

---

## Phase 4: User Story 3 - A deliberate placeholder (P3)

- [X] T009 [US3] Replace the letter tile with the aperture on an id-derived tint in `client/src/components/product/product-image/index.tsx`, also on image load failure

---

## Phase 5: User Story 4 - The product page (P3)

- [X] T010 [US4] Put the picture on its own panel and add "Sold by" in `client/src/pages/product/index.tsx`
- [X] T011 [P] [US4] Make each variant a whole clickable card, keeping the labelled radio, in `client/src/components/product/variant-chooser/index.tsx`

---

## Phase 6: User Story 5 - Remove an unused category (P2)

- [X] T012 [P] [US5] Write `DeleteCategoryTests` in `server/tests/Ecommerce.Catalog.Tests/DeleteCategoryTests.cs`: empty removed; with products refused with the count; emptied then removable; missing is 404
- [X] T013 [US5] Add `Remove(Category)` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/ICategoryRepository.cs` and implement it in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/CategoryRepository.cs`
- [X] T014 [US5] Add `CountInCategoryAsync` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs` and `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`
- [X] T015 [US5] Implement `DeleteCategoryCommand` and handler in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Commands/DeleteCategory/DeleteCategoryCommand.cs`: 404, 409 with the count, else remove and save
- [X] T016 [US5] Add `DELETE {id:guid}` with `[Authorize(Roles = "Admin")]` to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/CategoriesController.cs`
- [X] T017 [US5] Teach `server/seed/clean-test-debris.py` to delete categories whose slug `cameras.json` does not name, keeping whatever the API refuses; run it (93 removed)

---

## Phase 7: Polish and verification

- [X] T018 [P] Force UTF-8 output in `server/seed/clean-test-debris.py` and `server/seed/seed-catalogue.py`, which crashed printing Vietnamese names on a Windows console
- [X] T019 Drive headless Chrome against the dev server and read screenshots at 1440px, 500px and the product page; fix the hero hole, the repeated featured product and the doubled arrow
- [X] T020 Probe the reported 430px overflow (`VIEW=500 SCROLL=500`), revert the two fixes and the comments asserting it, and make the bar `rounded-3xl sm:rounded-full` for the real wrap at 500px
- [X] T021 [P] Update the Catalog test count (86) in `CLAUDE.md`
- [X] T022 Run lint, type-check, build and every test project (255 pass)
- [X] T023 Write this design record retrospectively under `specs/025-storefront-redesign/` (2026-09-27)
- [X] T024 Merged as [#62](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/62) (`902e411`) on 2026-09-22

---

## Dependencies & Execution Order

- T001 first: every component reads the tokens.
- US1-US4 touch different client files and can proceed in parallel after T001; T019-T020 need them all.
- US5 is independent of the client work; T017 needs T016.

## Notes

- 24 tasks: 2 foundational, 4 for US1, 2 for US2, 1 for US3, 2 for US4, 6 for US5, 7 polish.
- 1 test task (T012) holding 4 tests; no client tests existed at this merge.
