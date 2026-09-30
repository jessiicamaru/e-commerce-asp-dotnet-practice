---
description: "Task list for A person downloads the data the shop holds about them"
---

# Tasks: A person downloads the data the shop holds about them

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared

- [ ] T001 `Ecommerce.Shared/PersonalData`: `MyDataResponse`, `WithheldTable`, `PersonalDataInventory`, and the model check used by the tests

## Phase 2: The six services (US1, US2)

- [ ] T002 [P] Identity: `/api/auth/me/data`, inventory, `MyDataTests` (with secrets never exported)
- [ ] T003 [P] Catalog: `/api/products/my-data`, inventory, `MyDataTests`
- [ ] T004 [P] Order: `/api/orders/my-data`, inventory, `MyDataTests`
- [ ] T005 [P] Cart: `/api/cart/my-data`, inventory, `MyDataTests`
- [ ] T006 [P] Payment: `/api/payments/my-data`, inventory, `MyDataTests`
- [ ] T007 [P] Activity: `/api/notifications/my-data`, inventory, `MyDataTests` (no staff identities, no snapshots)

## Phase 3: Storefront

- [ ] T008 "Download my data" on `/account`: fetch the six, compose, download, name what was unavailable; words; tests

## Phase 4: Verification and docs

- [ ] T009 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [ ] T010 Bruno (six requests, six 401s); rebuilt containers; the post-design Constitution re-check
- [ ] T011 Docs: a personal-data page (the inventory), account docs, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T012 Merged as #231, part of #217
