---
description: "Task list for Orders are recognisable"
---

# Tasks: Orders are recognisable

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US2)

- [x] T001 Line previews on four queries; sales filtered to the caller
- [x] T002 Order tests: three at most, biggest first, seller's own only

## Phase 2: Storefront (US1-US3)

- [x] T003 `orderReference`, `OrderStatusChip`, `OrderRow`
- [x] T004 Four lists use the row
- [x] T005 Reference with copy on both order pages; status on the staff page
- [x] T006 Vitest

## Phase 3: Verification and docs

- [x] T007 Mutations, each red
- [x] T008 Bruno; rebuilt Order; checked in a browser
- [x] T009 Docs, generate_reference.py, timeline, backlog
- [x] T010 Merged, closes #248 - #272

## Evidence

**2026-10-02**

- **Order** (PostgreSQL): `OrderPreviewTests` (4) - the customer list, the fulfilment queue and the staff search name the
  three biggest lines of four; a seller's sale names only their own three biggest, even with another seller's bigger
  line on the order. Order 349/349.
- **Mutations, each red**: in each of the four queries, four lines instead of three and smallest first; another
  seller's lines on a sale; on the client, a six-character reference, one name instead of three, the partly-shipped
  chip ignored. `Take(4)` first survived on the sales query, whose test had only two of the seller's lines - tightened
  to four. The first ordering by id was wrong: ids made in one millisecond do not keep the order bought.
- **Vitest** 679/679; **Playwright flows** 5/5 (the seller opens the sale by its reference); **Bruno** 392/392 against
  the rebuilt Order (`lines` on the customer list and the seller's sales).
- **Browser** as the administrator: Find an order lists reference, chip, names and customer; an order page shows
  "Order 01a0faa5" with copy and a Shipped chip.
- **Post-design Constitution re-check**: unchanged.
