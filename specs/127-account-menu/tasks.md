---
description: "Task list for One account menu everywhere"
---

# Tasks: One account menu everywhere

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: One list (US1)

- [x] T001 `accountDestinations`
- [x] T002 The user menu, the phone menu and the account page draw it
- [x] T003 Account-area pages left-aligned
- [x] T004 Vitest: the three agree for a customer, a seller, staff

## Phase 2: Verification and docs

- [x] T005 Mutations, each red
- [x] T006 Timeline, backlog, client README
- [x] T007 Merged, closes #254 - #266

## Evidence

**2026-10-01**

- **Vitest** (10): for a customer, a seller and an administrator, the avatar menu, the phone menu and `/account` list
  exactly `accountDestinations` in its order; the list includes Saved, Notifications, two-factor sign-in and Open a shop.
- **Mutations, each red**: Notifications dropped from the list; the phone menu cut to four; the avatar menu ignoring
  the seller role; the account page missing Saved.
- **Client**: oxlint clean, Vitest 652/652, build green.
- **Post-design Constitution re-check**: unchanged.
