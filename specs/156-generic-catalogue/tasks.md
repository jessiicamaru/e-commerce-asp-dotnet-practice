---
description: "Task list for A shop for anything, not only cameras"
---

# Tasks: A shop for anything, not only cameras

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## User Story 1 - The shop's own words fit whatever it sells

- [x] T001 Reword `common.searchPlaceholder`, `catalog.hero.eyebrow`/`heading`, `seller.apply.subtitle`,
  `seller.addVariant.hint` in `vi` and `en`
- [x] T002 `ProductImage`: the parcel tile in place of the aperture
- [x] T003 Wording test over `common`/`catalog`/`seller`; shown failing with the old placeholder

## User Story 2 - A demo catalogue across several kinds of goods

- [x] T004 `seed/catalogue.py`: load every vertical, validate, run as a checker; shown rejecting each planted defect
- [x] T005 `cameras.json` moved to `seed/catalogue/`; five new verticals
- [x] T006 `seed-catalogue.py` and `clean-test-debris.py` read through the loader; seeding one vertical by name
- [x] T007 CI runs the checker
- [x] T008 Seeded against the compose stack twice; the cleaner proposes nothing seeded; the storefront checked

## Polish

- [x] T009 Docs: seed README, getting started, demo script, troubleshooting, project overview, catalog, CLAUDE.md,
  timeline, backlog
- [x] T010 Merged, closes #359 (#360)

## Evidence

Verified 2026-10-08 against the compose stack (storefront image rebuilt from this branch).

- **Wording**: the landing page reads "Many shops, one cart - Find the thing you came for." and "Search products…"; in
  Vietnamese "Nhiều gian hàng, một giỏ hàng - Tìm đúng thứ bạn cần." and "Tìm sản phẩm…". Products with no photograph
  show the parcel tile.
- **Wording test**: passes; with `"Search cameras…"` put back it fails naming `searchPlaceholder`; restored, it passes.
- **Checker**: six verticals, 41 products, 77 variants, exit 0. Each of eight planted defects is caught and named by
  file - a duplicate SKU across files, missing English text, an unknown category, a dong price with a fraction, variants
  naming different options, a duplicate slug, a three-decimal dollar price, negative stock; unplanted, nothing.
- **Seeding** (empty stack): 41 products added with no error; a second run: 0 added, 41 already there. The cleaner, dry
  run: 41 seeded products to keep, 0 to delete.
- **Through the API**: 13 categories in English; the Shoes filter in dollars returns Biti's Hunter X (45), Converse
  Chuck 70 Hi (90) and Nike Air Force 1 '07 (115), all in stock.
