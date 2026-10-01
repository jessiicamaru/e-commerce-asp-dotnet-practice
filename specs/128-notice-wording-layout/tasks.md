---
description: "Task list for Notice wording is edited like the emails"
---

# Tasks: Notice wording is edited like the emails

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Layout (US1)

- [x] T001 Kind list with names, edited mark and search; language switch; the chosen kind's editors
- [x] T002 The choice in the address
- [x] T003 Names for every kind in en and vi, held by a test
- [x] T004 Vitest

## Phase 2: Verification and docs

- [x] T005 Mutations, each red
- [x] T006 Checked in a browser
- [x] T007 Timeline, backlog
- [x] T008 Merged, closes #250 - #267

## Evidence

**2026-10-01**

- **Vitest** (9): the chosen kind in the chosen language, plural forms, the language switch; the address opens a kind
  in a language; search by name; every kind in `notification-kinds.json` named in en and vi; the editor's tests.
- **Mutations, each red**: every kind drawn at once; the address ignored; search by code only; a Vietnamese name
  missing.
- **Browser** as the administrator: `/admin/notifications?kind=AccountBanned&lang=vi` is 1,072px tall, from 7,028px.
- **Client**: oxlint clean, type-check clean, Vitest 655/655, build green.
- **Post-design Constitution re-check**: unchanged.
