---
description: "Task list for A seller pauses their shop, and staff close one"
---

# Tasks: A seller pauses their shop, and staff close one

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Catalog

- [x] T001 `Seller` columns, configuration and migration `AddShopPauseAndClosure`
- [x] T002 `SellerRepository.ApplyShopStateAsync`; the ban path uses it; approval in `TryReviewAsync` takes the shop's state
- [x] T003 [US1] Seller pause and reopen: repository moves, commands, audit, saver notices, and the `mine` routes
- [x] T004 [US2] Staff close and reopen, with notices (`ShopClosed`, `ShopReopened` in `notification-kinds.json`), plus the closed list and the routes
- [x] T005 `GetShopQuery` gains `Paused`; a closed shop is a 404
- [x] T006 `ShopClosureTests`; extend `SellerSuspensionTests` and `ShopPageTests`

## Phase 2: Storefront

- [x] T007 [US1] The shop card on `/shop`; the paused banner on `/shops/:id`
- [x] T008 [US2] The staff close dialog on `/shops/:id`; the "Closed shops" tab on `/admin/shops`; notice wording; tests

## Phase 3: Verification and docs

- [x] T009 Mutations (quickstart Scenario 3), each red; the full Catalog and client suites
- [x] T010 [P] Bruno; a rebuilt Catalog and storefront; the post-design Constitution re-check
- [x] T011 Docs: marketplace, moderation-and-staff, catalog, audit-and-notifications, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T012 Merged as #227, closing #214

## Evidence

- Catalog 250/250, including `ShopClosureTests` 9/9; client 548/548, `oxlint` and `tsc -b` clean.
- Catalog mutations, each red then restored: the shelf statement ignoring the pause; the ban path writing its own
  bool again; approval not taking the shop's state; the seller's reopen or pause without `"ClosedAt" IS NULL`; staff
  reopen clearing the pause; the public page returning a closed shop or not flagging a paused one; no saver notice on
  reopening; no closure notice; closure audited as Catalog.
- Client mutations, each red: Reopen offered for a closed shop; pausing without the confirming action; no paused
  banner; Close offered to a customer; the reason sent untrimmed; the Closed tab showing applications; the reason
  hidden from the seller.
- Bruno against rebuilt containers: 340/340 requests, 548/548 tests. ⚠️ The first run found the six routes missing:
  the controller edit had silently not applied to a CRLF file, and every unit test passed, because they send
  commands, not requests. The same run found `admin-audit/staff see the received return` depending on this run's
  return being among the first fifty received; it now pages.
- Post-design Constitution re-check: see [plan.md](plan.md) - no violation found.
