---
description: "Task list for A Catalogue of Real Cameras"
---

# Tasks: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Input**: Design documents from `/specs/023-camera-catalogue/` (reconstructed)

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included for the two Catalog defects (`VariantTests`, real PostgreSQL). The seeder itself has
no automated test; it was run against the stack and its rows read back.

**Reconstructed**: no task list existed at the merge. These tasks describe what the pull request's diff
and description show was done, in a dependency order; the order in which it was actually done is not
recorded.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (real cameras), US2 (one command, repeatable), US3 (one option order), US4 (option ids)

## Phase 1: The data

- [X] T001 [US1] Write `server/seed/cameras.json`: 2 categories (`may-anh-mirrorless`, `may-anh-compact`), 14 products, 23 variants, each variant with options in Vietnamese and English, a dong price, a dollar price and stock
- [X] T002 [US1] Put the approximate-price warning and the two-lists note in `_about` at the top of `server/seed/cameras.json` (research D3)

## Phase 2: The seeder (US1, US2)

- [X] T003 [US2] `server/seed/seed-catalogue.py`: sign in as `ADMIN_EMAIL` / `ADMIN_PASSWORD` through the gateway (`GATEWAY_URL`, default `:5000`); every request with `Accept-Language: vi` and `X-Currency: VND`
- [X] T004 [US2] Categories by slug, products by product SKU, variants by variant SKU; treat the first variant as the product's own (specs/020) so it is never added twice
- [X] T005 [US1] Per product: English text (`PUT .../translations/en`); per variant: the dollar price (`PUT .../prices/USD`) and stock (`PUT /api/stock/{variantId}`)
- [X] T006 [US2] Wait for Inventory: retry a stock write's 404 up to 20 times, 0.5 s apart, then stop with a message naming the variant (research D8)
- [X] T007 [US2] Never delete; stop on any unexpected status with the method, path, status and the start of the body; print the no-images note

## Phase 3: US4 - an option can be addressed

- [X] T008 [P] [US4] `VariantOptionResponse(Guid Id, string Name, string Value)` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/VariantResponse.cs`
- [X] T009 [P] [US4] `options: { id; name; value }[]` in `client/src/services/product/types.ts`
- [X] T010 [US4] Test `An_option_carries_its_id_so_it_can_be_addressed` in `server/tests/Ecommerce.Catalog.Tests/VariantTests.cs`
- [X] T011 [US1] Translate each variant's options through `PUT /api/products/{id}/options/{optionId}/translations/en`, reading the option ids from the product read back

## Phase 4: US3 - one option order

- [X] T012 [US3] Order by name, case-insensitive, in `ProductVariant.Summarise` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductVariant.cs`
- [X] T013 [US3] Order by the **stored** name in the translated summary in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/Localized.cs`
- [X] T014 [US3] Data-only migration `20260922161858_NormaliseOptionSummaryOrder` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/`, rewriting every stored `OptionSummary`; `Down` deliberately empty
- [X] T015 [US3] Test `Two_shapes_of_one_product_list_their_options_in_the_same_order` in `VariantTests.cs`, entering two variants' options in opposite orders; verified to go red with the fix removed

## Phase 5: The seeder's own bug, and proof

- [X] T016 [US1] Match each option to its English words **by name**, not by position, in `translate_options` - the first version gave `Bộ: Chỉ thân máy` the English `Colour: Black`; caught by reading the rows (research D7)
- [X] T017 [US2] Run the seeder twice against the stack: the second run reports `0 product(s) added, 14 already there`
- [X] T018 [US1] Read the rows back in `vi`/`VND` and `en`/`USD`
- [X] T019 Run the whole suite (245), Bruno (81/81, 122 tests), `verify-auth.sh` and `verify-saga.sh`
- [X] T020 [P] Document seeding and the price warning in `CLAUDE.md`
- [X] T021 PR [#60](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/60) "feat(catalog): a catalogue of real cameras, seeded through the API"; CI green (build and test, schema compatibility, storefront build, image scan, auth smoke, saga end-to-end); squash-merged as `06ad8e3` on 2026-09-22

## Dependencies & Execution Order

- T001-T002 before the seeder can run anything.
- T008 (the option id) blocks T011 (translating options): without it the endpoint cannot be called.
- T012-T014 are independent of the seeder, but the defect was found by it.
- T016 follows T011 - it is the fix to T011's first version.
- T017-T019 need everything deployed.

## Notes

- 21 tasks, all done. 2 are automated tests (T010, T015); T017-T019 are the recorded verification.
- The pull request's checks, read on 2026-09-27: every job green; "Publish images" skipped, as on every pull request.
