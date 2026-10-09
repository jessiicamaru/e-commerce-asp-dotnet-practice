---
description: "Task list for A cart before signing in"
---

# Tasks: A cart before signing in

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 Extract `CartPricing` from `GetMyCartQueryHandler`; the stored cart reads through it unchanged
- [ ] T002 `POST /api/cart/price` (anonymous, stores nothing) and `POST /api/cart/merge` (larger quantity, one
  transaction); validators (50 lines, 1-999)
- [ ] T003 `GuestCartTests` on PostgreSQL: pricing equal to the stored cart's and writing nothing, the merge rule, a
  repeated merge, refusals; a mutation

## Client

- [ ] T004 The browser store (`utils/cart/guest-cart`), service methods, hooks reading either cart
- [ ] T005 Add to cart signed out; the header's count; `/cart` open signed out, checkout to sign-in and back
- [ ] T006 The merge wherever a sign-in ends
- [ ] T007 Client tests; locales

## Contract, docs

- [ ] T008 Bruno for both endpoints and their refusals; `docs/reference` regenerated
- [ ] T009 On the stack: the quickstart and the journey in a browser
- [ ] T010 Docs: the cart's page, CLAUDE.md, timeline, backlog
- [ ] T011 Merged, closes #370

## Evidence

(Filled in when the work is verified.)
