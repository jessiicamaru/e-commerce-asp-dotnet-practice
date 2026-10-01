---
description: "Task list for A signed-out shopper is offered Add to cart"
---

# Tasks: A signed-out shopper is offered Add to cart

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Buying signed out (US1)

- [x] T001 `AddToCart` for everybody; signed out it goes to sign-in with the address and a reason
- [x] T002 The product page reads `?variant=`
- [x] T003 The sign-in page words the reason
- [x] T004 Vitest

## Phase 2: The SKU (US2)

- [x] T005 Only for a chosen variant
- [x] T006 Vitest

## Phase 3: Verification and docs

- [x] T007 Mutations, each red
- [x] T008 Checked in a browser
- [x] T009 Timeline, backlog
- [x] T010 Merged, closes #252 - #265

## Evidence

**2026-10-01**

- **Vitest**: signed out, Add to cart opens sign-in with `{ from: '/products/p1?variant=kit', reason: 'cart' }`;
  `/products/p1?variant=kit` comes back with that kit chosen; no SKU until a variant is chosen; the sign-in page's two
  reasons and none on its own.
- **Mutations, each red**: the choice dropped from `from`; `?variant=` ignored; the first variant's SKU back; the
  reason not shown (2 red).
- **Browser** (Edge, Vite dev): signed out on a camera with kits, the second chosen, Add to cart → sign-in with the
  reason → signed in → back on the product with `?variant=` and the second kit checked.
- **Decided**: specs/020 research D10 kept - no preselected variant (research D1).
- **Client**: oxlint clean, type-check clean, Vitest 642/642 (one run had a single flaky failure that did not recur),
  build green.
- **Post-design Constitution re-check**: unchanged.
