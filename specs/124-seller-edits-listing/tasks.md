---
description: "Task list for A seller edits what they listed"
---

# Tasks: A seller edits what they listed

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [ ] T001 `UpdateProductDetailsCommand` + validator + `PUT /api/products/{id}`
- [ ] T002 `translatedLanguages` on the lookup
- [ ] T003 Catalog tests: owner, other seller 404, admin, review move, category, nothing changed, response

## Phase 2: Storefront (US1-US4)

- [ ] T004 Details card
- [ ] T005 Translations card
- [ ] T006 Add-variant form
- [ ] T007 New-product form: submit at the end, honest hint
- [ ] T008 Vitest for each

## Phase 3: Verification and docs

- [ ] T009 Mutations, each red
- [ ] T010 Bruno requests; rebuilt Catalog; the page in a browser
- [ ] T011 Catalog/marketplace docs, CLAUDE.md, generate_reference.py, audit label, timeline, backlog
- [ ] T012 Merged, closes #240

## Evidence

(Filled in when the work is verified.)
