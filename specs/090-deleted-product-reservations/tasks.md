---
description: "Task list for A deleted product's holds are released with its stock"
---

# Tasks: A deleted product's holds are released with its stock

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - three failed before the fix.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Establish (the issue's first request)

- [X] T001 Read every settlement path for a missing stock row - confirm, release, expire, both restocks (spec table)

## Phase 2: Tests first

- [X] T002 [US1] `server/tests/Ecommerce.Inventory.Tests/ForgetProductTests.cs`: held released with its stock; mixed order; sweeper finds nothing
- [X] T003 [US2] Same file: a settled reservation unchanged; a later cancellation settles without a shelf
- [X] T004 Run before the fix: the three US1 tests red, the two US2 tests green (they pin existing behaviour)

## Phase 3: Implementation (US1)

- [X] T005 [US1] `IReservationRepository.ReleaseHeldAsync` and its `ExecuteUpdateAsync` in `ReservationRepository.cs`
- [X] T006 [US1] `ForgetProductCommandHandler`: one transaction, forget then release; `Reason` constant; log both counts
- [X] T007 [P] Correct the "reservations cascade" comment in `StockRepository.ForgetAsync`

## Phase 4: Verification and docs

- [X] T008 Mutations (quickstart Scenario 3) - each red; `Ecommerce.Inventory.Tests` 57/57
- [X] T009 Docs: `docs/features/catalog.md`, `docs/features/shopping-and-checkout.md`, `docs/project/backlog.md`, `docs/project/timeline.md`, CLAUDE.md
- [X] T010 Merged as #189 (2026-09-27), closing #181

## Verification

- Before: `A_held_reservation_of_a_deleted_variant_is_released_with_its_stock`,
  `Only_the_deleted_variants_holds_are_released_and_the_rest_of_the_order_settles` and
  `The_sweeper_finds_nothing_of_a_deleted_variant_to_expire` red (3 of 10 in `ForgetProductTests`).
- After: `Ecommerce.Inventory.Tests` 57/57.
- Mutations, each restored: no release - the three US1 tests red; no `Held` guard -
  `A_settled_reservation_stays_as_the_orders_history` red.
