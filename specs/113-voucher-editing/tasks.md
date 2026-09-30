---
description: "Task list for A voucher's terms can be corrected"
---

# Tasks: A voucher's terms can be corrected

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2, US3)

- [ ] T001 `EditVoucherCommand` + validator; `IVoucherRepository.TryEditAsync` - the guarded statement, the amounts and the condition, the audit stage, one transaction
- [ ] T002 `PUT /api/vouchers/{id}`
- [ ] T003 `VoucherEditingTests`: extended end date and an earlier order untouched; limit under uses 409; minimum lowered qualifies a checkout; disabled 409; other seller 404; administrator on a seller's 404; audit before/after; an edit racing claims

## Phase 2: Storefront (US1)

- [ ] T004 "Edit" on an active voucher in the shared voucher page: a dialog prefilled with the terms; words; tests

## Phase 3: Verification and docs

- [ ] T005 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [ ] T006 Bruno (edit, 409, 403); rebuilt Order container; the post-design Constitution re-check
- [ ] T007 Docs: vouchers page, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T008 Merged as #233, closes #219
