---
description: "Task list for Long staff lists can be searched and filtered"
---

# Tasks: Long staff lists can be searched and filtered

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [x] T001 Voucher search and state
- [x] T002 User role and state
- [x] T003 Order and Identity tests

## Phase 2: Storefront (US1-US4)

- [x] T004 Voucher page search and counted state tabs
- [x] T005 Users role and state filters
- [x] T006 Categories search and paging
- [x] T007 Find an order: worded failures, tab counts
- [x] T008 Vitest

## Phase 3: Verification and docs

- [x] T009 Mutations, each red
- [x] T010 Bruno; rebuilt Order and Identity; checked in a browser
- [x] T011 Docs, generate_reference.py, timeline, backlog
- [x] T012 Merged, closes #249 - #273

## Evidence

- **Server, against PostgreSQL**:
  - `VoucherFilterTests` (3) and `UserFilterTests` (3).
  - Order 352/352, Identity 257/257.
- **Server mutations, each red**:
  - V1: the voucher search ignored.
  - V2: the Ended state not excluding disabled vouchers.
  - U1: the role filter dropped.
  - U2: Locked counting a lock that has run out.
  - U3: Active including the banned.
- **Client**: Vitest 686/686.
  - One run of the full suite hit the 15 s limit in "creates one for the shop". That test takes 3.5 s on its own; the rerun of the full suite was green.
- **Client mutations, each red**:
  - C1: the voucher state ignored.
  - C2: the tab counts hidden.
  - C3: the users role dropped.
  - C4: the users state dropped. It first survived because no test read the service's query string, so `services/accounts/index.test.ts` was added.
  - C5: categories not filtered.
  - C6: categories not paged.
  - C7: the order tab counts hidden.
  - C8 and C9: the voucher search and state dropped from the URL.
- **Bruno**: 399/399 (651 tests) against rebuilt Order and Identity containers, including `admin-users/` 41-44 and `seller/` 121-123.
- **Browser**: Edge against the live stack (Vite :5173). Checked: users by Locked (34 people) and by Moderator, categories searched for "lens", voucher tabs All 63 / Active 63 / Ended 0 / Turned off 0, and Find an order tabs All 127 … Failed 3 … Cancelled 12.
