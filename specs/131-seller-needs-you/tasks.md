---
description: "Task list for A seller's home says what needs them"
---

# Tasks: A seller's home says what needs them

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [x] T001 `status` on the sales query, validated; filtered in SQL
- [x] T002 Order tests: each status, totals across pages, cancelled excluded from Paid, 400

## Phase 2: Storefront (US1, US2)

- [x] T003 `useSellerWaiting`; Needs you panel; menu badges
- [x] T004 Labels
- [x] T005 Vitest

## Phase 3: Verification and docs

- [x] T006 Mutations, each red
- [x] T007 Bruno; rebuilt Order; the page in a browser
- [x] T008 Marketplace/fulfilment docs, generate_reference.py, timeline, backlog
- [x] T009 Merged, closes #247 - #271

## Evidence

**2026-10-02**

- **Order** (PostgreSQL): `SalesFilterTests` - three sales to prepare counted with a page of one; Preparing 1,
  Shipped 2, Cancelled 2 (a cancelled part and a cancelled order), all 8 with no filter; another seller's waiting part
  on an order holding this seller's shipped part does not count as this seller's to prepare; `Delivered` is 400 on
  `Status`. Order 345/345.
- **Mutations, each red**: a cancelled part counted as to prepare; a cancelled order counted; no filter; any status
  accepted; on the client, the count unfiltered, the missing payout account hidden, no menu badges, the panel off the
  home.
- **Vitest** 671/671 - the panel, the empty message, the home before anything is listed, the menu badges asked with
  `status=Paid`.
- **Browser** (Playwright flows, Edge): after the customer pays, the seller's `/shop` shows "1 sale to prepare" and the
  menu "1 waiting" - 5/5.
- **Bruno** against the rebuilt Order: `status=Paid` with a page of one, `status=Delivered` 400 - 392/392 requests.
- **Post-design Constitution re-check**: unchanged.
