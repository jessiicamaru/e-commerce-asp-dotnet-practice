---
description: "Task list for A shop for anything, not only cameras"
---

# Tasks: A shop for anything, not only cameras

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## User Story 1 - The shop's own words fit whatever it sells

- [ ] T001 Reword `common.searchPlaceholder`, `catalog.hero.eyebrow`/`heading`, `seller.apply.subtitle`,
  `seller.addVariant.hint` in `vi` and `en`
- [ ] T002 `ProductImage`: the parcel tile in place of the aperture
- [ ] T003 Wording test over `common`/`catalog`/`seller`; shown failing with the old placeholder

## User Story 2 - A demo catalogue across several kinds of goods

- [ ] T004 `seed/catalogue.py`: load every vertical, validate, run as a checker; shown rejecting each planted defect
- [ ] T005 `cameras.json` moved to `seed/catalogue/`; five new verticals
- [ ] T006 `seed-catalogue.py` and `clean-test-debris.py` read through the loader; seeding one vertical by name
- [ ] T007 CI runs the checker
- [ ] T008 Seeded against the compose stack twice; the cleaner proposes nothing seeded; the storefront checked

## Polish

- [ ] T009 Docs: seed README, getting started, demo script, troubleshooting, project overview, catalog, CLAUDE.md,
  timeline, backlog
- [ ] T010 Merged, closes #359

## Evidence

(Filled in when the work is verified.)
