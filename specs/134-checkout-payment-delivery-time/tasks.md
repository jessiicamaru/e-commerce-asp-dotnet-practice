---
description: "Task list for Checkout says how payment works and how long delivery takes"
---

# Tasks: Checkout says how payment works and how long delivery takes

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [ ] T001 `MinDays`/`MaxDays` on `DeliveryOption`, configuration, the CHECK, migration `DeliveryEstimate`
- [ ] T002 Seeding and startup validation of `Shipping:Options:N:MinDays/MaxDays`; appsettings estimates
- [ ] T003 Save command, validator, responses; shipping options and the quote carry the estimate
- [ ] T004 `DeliveryEstimateTests` against PostgreSQL

## Phase 2: Storefront (US1-US3)

- [ ] T005 The estimate under each option at checkout
- [ ] T006 The Payment card, its stand-in line from Payment's health
- [ ] T007 `/admin/delivery` edits the estimate
- [ ] T008 Vitest

## Phase 3: Verification and docs

- [ ] T009 Mutations, each red
- [ ] T010 Bruno; rebuilt Order; checked in a browser
- [ ] T011 Docs, generate_reference.py, timeline, backlog
- [ ] T012 Merged, closes #253

## Evidence

(Filled in when the work is verified.)
