---
description: "Task list for Shoppers see the vouchers they could use"
---

# Tasks: Shoppers see the vouchers they could use

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2, US3)

- [x] T001 `vouchers.IsPublic` + migration `AddVoucherVisibility`; `isPublic` on create and edit; `VoucherSummary.IsPublic`
- [x] T002 `GetPublicVouchersQuery` + validator + repository read; `GET /api/vouchers/public` (anonymous)
- [x] T003 `OrderItemResponse.SellerId` on quote and order lines
- [x] T004 `PublicVoucherTests`: each filter, the scopes, the product targets, the order, the hidden counts, create and edit

## Phase 2: Storefront (US1, US2, US3)

- [x] T005 "Show it to shoppers" on create and edit; shown/code-only in the owner's list
- [x] T006 `PublicVouchers` list with a copy of the code - on the shop page and the product page; at checkout with "Use"; words; tests

## Phase 3: Verification and docs

- [x] T007 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [x] T008 Bruno (public, then private; the 400); rebuilt Order container; the post-design Constitution re-check
- [x] T009 Docs: vouchers page, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T010 Merged as #234, closes #220

## Evidence (2026-10-01)

- **Server**: `PublicVoucherTests` (8) against PostgreSQL - a public shop voucher listed for its shop and a private one
  never; ended, not started, disabled, used up and unpriced-in-the-currency ones not listed (and a dollar voucher listed
  in dollars); the platform's only when asked; a product sees vouchers for everything, naming it, or a given variant;
  ending soonest first, open-ended last; no `TotalLimit`, `UsedCount` or `PerCustomerLimit` in the answer; no scope 400;
  shown on create, hidden by edit, kept by an edit that does not say; the quote's lines carry their seller.
- **Client**: 602/602 (103 files), oxlint and `tsc -b` clean - the list asks for its scope and words each voucher, draws
  nothing when empty and asks nothing without a scope, offers unused ones to use at checkout; the service's address;
  the shop page asks for its shop; checkout asks for the platform and the cart's shops and "Use" quotes with the code.
- **Mutations, 17, every one red**: the public, status, started, ended, used-up and currency filters; the platform only
  when asked; the shop filter; the product filter; the variant targets; the order; create and edit ignoring the flag; an
  edit that does not say clearing it; the quote without sellers; in the storefront, used codes offered again and the
  cart's shops not asked about.
- **Bruno** against the rebuilt Order container: 383/383 requests, 619/619 tests - the seller's voucher created public
  and listed without a token, edited to code only and gone; no scope 400. `seller/` requests from 66 on moved up by 2.
- **Post-design Constitution re-check**: unchanged - Order's own table, one anonymous read that names nobody, the flag
  written by the create and edit that already commit with their audit entries.
