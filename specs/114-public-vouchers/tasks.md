---
description: "Task list for Shoppers see the vouchers they could use"
---

# Tasks: Shoppers see the vouchers they could use

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2, US3)

- [ ] T001 `vouchers.IsPublic` + migration `AddVoucherVisibility`; `isPublic` on create and edit; `VoucherSummary.IsPublic`
- [ ] T002 `GetPublicVouchersQuery` + validator + repository read; `GET /api/vouchers/public` (anonymous)
- [ ] T003 `OrderItemResponse.SellerId` on quote and order lines
- [ ] T004 `PublicVoucherTests`: each filter, the scopes, the product targets, the order, the hidden counts, create and edit

## Phase 2: Storefront (US1, US2, US3)

- [ ] T005 "Show it to shoppers" on create and edit; shown/code-only in the owner's list
- [ ] T006 `PublicVouchers` list with a copy of the code - on the shop page and the product page; at checkout with "Use"; words; tests

## Phase 3: Verification and docs

- [ ] T007 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [ ] T008 Bruno (public, then private; the 400); rebuilt Order container; the post-design Constitution re-check
- [ ] T009 Docs: vouchers page, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T010 Merged as #234, closes #220
