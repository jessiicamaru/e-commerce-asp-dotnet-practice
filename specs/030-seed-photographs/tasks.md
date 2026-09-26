---
description: "Task list for Seed Photographs"
---

# Tasks: Seed Photographs

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Input**: Design documents from `/specs/030-seed-photographs/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: None automated. The script was verified by running it against the stack and counting the files
it left.

Reconstructed from the merge diff and the PR; every task below is in #69.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US3)

---

## Phase 1: Setup

- [X] T001 [US3] Add `server/seed/images/` to `.gitignore`, with the reason: the repository is public and a photograph belongs to whoever took it

---

## Phase 2: User Story 1 - Put photographs on the catalogue (P1) 🎯 MVP

- [X] T002 [US1] Write `server/seed/seed-images.py`: sign in, read `/api/products?pageSize=200`, match `seed/images/*` by stem = SKU, dry run by default, upload with `--yes` as multipart part `file` to `PUT /api/products/{id}/image`
- [X] T003 [US1] Report per file (ok, skipped, failed, "replaces the current image" or "first image"), list products with no file, print the totals and exit 1 on any failure, in `server/seed/seed-images.py`

---

## Phase 3: User Story 2 - Refuse what Catalog would refuse (P2)

- [X] T004 [US2] Detect JPEG, PNG and WebP by magic bytes and refuse anything else or anything over 2 MB before sending, in `server/seed/seed-images.py`

---

## Phase 4: User Story 3 - No photograph committed, every credit written (P1)

- [X] T005 [P] [US3] Choose photographs on Wikimedia Commons, looking at each in a contact sheet (rejecting `Harbor rope.jpg` and the 2011 X100), and keep the ZV-E10 II's plainer photograph over the previous generation's
- [X] T006 [P] [US3] Write `server/seed/IMAGE-CREDITS.md`: SKU, file, photographer, licence for 13 Commons photographs; `FUJI-X100VI` recorded as an unlicensed local placeholder; the two warnings for whoever re-runs it
- [X] T007 [P] [US3] Write `server/seed/README.md`: how to run the seeder, the naming rule, the licensing rule, and the re-run behaviour

---

## Phase 5: Verification

- [X] T008 Run the seeder against the stack: 14 uploaded, 0 skipped, 0 failed; count the files on the `catalog_images` volume: 14 after replacing 11, not 25
- [X] T009 Write this design record retrospectively under `specs/030-seed-photographs/` (2026-09-27)
- [X] T010 Merged as [#69](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/69) (`dfd63b7`) on 2026-09-23

---

## Dependencies & Execution Order

- T001 before any image is staged in the working tree.
- T002-T004 are one file, in order.
- T005 before T006 and T008.

## Notes

- 10 tasks: 1 setup, 2 for US1, 1 for US2, 3 for US3, 3 verification.
- No test task: the PR records no automated test, and none was written for this record.
