---
description: "Task list for A person downloads the data the shop holds about them"
---

# Tasks: A person downloads the data the shop holds about them

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared

- [x] T001 `Ecommerce.Shared/PersonalData`: `MyDataResponse`, `WithheldTable`, `PersonalDataInventory`, and the model check used by the tests

## Phase 2: The six services (US1, US2)

- [x] T002 [P] Identity: `/api/auth/me/data`, inventory, `MyDataTests` (with secrets never exported)
- [x] T003 [P] Catalog: `/api/products/my-data`, inventory, `MyDataTests`
- [x] T004 [P] Order: `/api/orders/my-data`, inventory, `MyDataTests`
- [x] T005 [P] Cart: `/api/cart/my-data`, inventory, `MyDataTests`
- [x] T006 [P] Payment: `/api/payments/my-data`, inventory, `MyDataTests`
- [x] T007 [P] Activity: `/api/notifications/my-data`, inventory, `MyDataTests` (no staff identities, no snapshots)

## Phase 3: Storefront

- [x] T008 "Download my data" on `/account`: fetch the six, compose, download, name what was unavailable; words; tests

## Phase 4: Verification and docs

- [x] T009 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [x] T010 Bruno (six requests, six 401s); rebuilt containers; the post-design Constitution re-check
- [x] T011 Docs: a personal-data page (the inventory), account docs, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T012 Merged as #231, part of #217

## Evidence (2026-10-01)

- **Server**: every suite green against PostgreSQL - Activity 42, ApiGateway 13, Cart 21, Catalog 260, Identity 235,
  Inventory 78, Orchestrator 18, Order 310, Payment 30. `MyDataTests`: 3 per service, plus Identity's check of
  `PersonalDataInventory.Problems` against each way a declaration can be wrong.
- **Client**: 580/580 (100 files), oxlint and `tsc -b` clean; the account page tests ask all six, save one file with the
  person and every answer, and mark and name a failed service.
- **Mutations, 21, every one red**: an address, review, return, cart or refund filter widened; orders widened to a
  seller's sales; products narrowed to the shop's own; a secret added to Identity's export (full account number, an
  email's data); a staff id added (review `HiddenBy`, payout `RecordedBy`, audit `ActorId`); an audit snapshot added;
  entries about the person dropped; the undeclared-table check disabled; a whitespace reason accepted; a table dropped
  from Catalog's inventory; a section dropped from Order's reader; and in the storefront a failed service not named,
  not marked, or one service not asked.
- **Bruno** against rebuilt Identity, Catalog, Order, Cart, Payment and Activity containers: 368/368 requests,
  601/601 tests - the new `my-data` folder (seq 16, so `seller` is 17 and `teardown` 18) and six 401s in
  `security-checks`. A first run had four `ECONNRESET`s on unrelated routes after the containers were recreated
  under a running gateway; a restart of the gateway cleared them.
- **Decided while building**: `voucher_customer_uses` is its own section, `voucherUseCounts`; Scenario 3's "Payment
  stopped" is covered by the page test rather than by stopping a container.
