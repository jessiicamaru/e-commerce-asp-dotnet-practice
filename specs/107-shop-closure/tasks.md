---
description: "Task list for A seller pauses their shop, and staff close one"
---

# Tasks: A seller pauses their shop, and staff close one

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Catalog

- [ ] T001 `Seller` columns, configuration and migration `AddShopPauseAndClosure`
- [ ] T002 `SellerRepository.ApplyShopStateAsync`; the ban path uses it; approval in `TryReviewAsync` takes the shop's state
- [ ] T003 [US1] Seller pause and reopen: repository moves, commands, audit, saver notices, and the `mine` routes
- [ ] T004 [US2] Staff close and reopen, with notices (`ShopClosed`, `ShopReopened` in `notification-kinds.json`), plus the closed list and the routes
- [ ] T005 `GetShopQuery` gains `Paused`; a closed shop is a 404
- [ ] T006 `ShopClosureTests`; extend `SellerSuspensionTests` and `ShopPageTests`

## Phase 2: Storefront

- [ ] T007 [US1] The shop card on `/shop`; the paused banner on `/shops/:id`
- [ ] T008 [US2] The staff close dialog on `/shops/:id`; the "Closed shops" tab on `/admin/shops`; notice wording; tests

## Phase 3: Verification and docs

- [ ] T009 Mutations (quickstart Scenario 3), each red; the full Catalog and client suites
- [ ] T010 [P] Bruno; a rebuilt Catalog and storefront; the post-design Constitution re-check
- [ ] T011 Docs: marketplace, moderation-and-staff, catalog, audit-and-notifications, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T012 Merged as #227, closing #214
