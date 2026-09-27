---
description: "Task list for Administrators manage categories"
---

# Tasks: Administrators manage categories

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the 409 test was red before the fix; the rename tests could not compile until the
command existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US2, US1)

- [X] T001 `CategoryAdminTests` (4) in `server/tests/Ecommerce.Catalog.Tests/`
- [X] T002 [US2] `UpdateCategoryCommand`, validator, handler (audited); `PUT /api/categories/{id}` (Admin)
- [X] T003 [US1] A duplicate slug is `ConflictException` in `CreateCategoryCommandHandler`

## Phase 2: Storefront (US1, US2)

- [X] T004 `Category` service calls, `useCategoriesIn`, `useCategoryChanges`, `slugOf`, query key
- [X] T005 [US1] [US2] `/admin/categories` (page, row), route (Admin), menu, words en/vi
- [X] T006 Page tests (7)

## Phase 3: Verification and docs

- [X] T007 Mutations (quickstart Scenario 4) - each red; Catalog 230/230; page 7/7
- [X] T008 [P] Bruno `category/` seq 3, 6, 7
- [X] T009 Rebuilt Catalog and the storefront; Bruno through the gateway
- [X] T010 Docs: `docs/features/catalog.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [X] T011 Merged as #204 (2026-09-27), closing #195
