---
description: "Task list for Checkout says how payment works and how long delivery takes"
---

# Tasks: Checkout says how payment works and how long delivery takes

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [x] T001 `MinDays`/`MaxDays` on `DeliveryOption`, configuration, the CHECK, migration `DeliveryEstimate`
- [x] T002 Seeding and startup validation of `Shipping:Options:N:MinDays/MaxDays`; appsettings estimates
- [x] T003 Save command, validator, responses; shipping options and the quote carry the estimate
- [x] T004 `DeliveryEstimateTests` against PostgreSQL

## Phase 2: Storefront (US1-US3)

- [x] T005 The estimate under each option at checkout
- [x] T006 The Payment card, its stand-in line from Payment's health
- [x] T007 `/admin/delivery` edits the estimate
- [x] T008 Vitest

## Phase 3: Verification and docs

- [x] T009 Mutations, each red
- [x] T010 Bruno; rebuilt Order; checked in a browser
- [x] T011 Docs, generate_reference.py, timeline, backlog
- [x] T012 Merged, closes #253 - #274

## Evidence

- **Server, against PostgreSQL**:
  - `DeliveryEstimateTests`: 12 cases.
  - Order 366/366.
  - The CHECK test found a real hole. The first constraint let one end through, because the CHECK evaluated to NULL and a CHECK passes on NULL. Each end is now named `IS NOT NULL`.
- **Server mutations, each red**:
  - S1: the handler keeps no time.
  - S2: the validator rule removed.
  - S3: soonest-after-latest allowed.
  - S4: the 60-day limit loosened.
  - S5: the list drops the time.
  - S6: the quote drops the time.
  - S7: the seed drops the time.
  - S8: configuration unchecked.
  - S9: the stored options drop the time.
  - S10: the null-unsafe CHECK.
- **Client**: Vitest 693/693.
- **Client mutations, each red**:
  - M1: no time shown.
  - M2: always a range.
  - M3: nulls shown.
  - M4: the stub line shown for a real provider.
  - M5: the stub line never shown.
  - M6: the provider ignored.
  - M7: an empty time sent as 0.
  - M8: the latest day not sent.
  - M9: no payment card.
  - M10: the stored time not shown in the form.
- **Bruno**: 403/403 (658 tests) against the rebuilt Order container, including `order/` 19-22. Express is given 1-2 days and listed with it, a time that is not one is a 400 naming `MaxDays`, and express is put back.
- **Browser**: Edge against the live stack. `/admin/delivery` set Standard to 3-5 and Express to 1-2. A new customer's `/checkout` reads "3–5 business days" and "1–2 business days", and the Payment card shows the stand-in note.
