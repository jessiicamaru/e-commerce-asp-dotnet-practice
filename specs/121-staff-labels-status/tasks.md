---
description: "Task list for Every audit action has words, and the status page lists every service"
---

# Tasks: Every audit action has words, and the status page lists every service

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Labels (US1)

- [x] T001 The 54 missing actions in en and vi
- [x] T002 Test: every recorded action has words; indirect calls declared

## Phase 2: Status (US2)

- [x] T003 Eight services by name; the note removed
- [x] T004 Test: the list equals the gateway's health routes

## Phase 3: Verification and docs

- [x] T005 Mutations, each red
- [x] T006 Checked in a browser
- [x] T007 Timeline, backlog, client README
- [x] T008 Merged, closes #244 - #260

## Evidence

**2026-10-01**

- **Counted**: 112 actions recorded across the services (literals, ternaries, and five indirect files: notice wording,
  product review, shop closure, email templates, returns); 54 had no label. All have words in en and vi now.
- **Vitest**: the audit test reads the server's source and finds every action labelled in both languages with no
  undeclared indirect call; the status list equals the gateway's eight `*-health-route` entries and each has a name in
  both languages.
- **Mutations, each red**: a Vietnamese label removed; the shop-closure file left undeclared; Activity dropped from the
  list; a Vietnamese service name removed.
- **Browser** (Edge, Vite dev, Vietnamese): `/status` lists eight services by name, all up; signed in as the
  administrator, `/admin/moderation` and `/admin/audit` show no raw action name.
- **Client**: oxlint clean, type-check clean, Vitest 621/621, build green.
- **Post-design Constitution re-check**: unchanged.
