---
description: "Task list for A compare-at price per variant"
---

# Tasks: A compare-at price per variant

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 The two columns, their CHECKs and the `AddCompareAtPrices` migration (expand-only)
- [ ] T002 Set and clear a compare-at: currency, representable, above the price, ownership, audit, no review
- [ ] T003 The price writers clear a compare-at they reach (research D2)
- [ ] T004 `compareAtPrice` on the variant and the product (research D4); `onSale` on the listing
- [ ] T005 `CompareAtPriceTests` on PostgreSQL, incl. the CHECK, the clearing, the filter and checkout's price; a mutation

## Client

- [ ] T006 Types, service, hooks; locales (incl. audit labels)
- [ ] T007 The struck-through price and the percentage on cards and the product page
- [ ] T008 The "On sale" filter
- [ ] T009 The seller's compare-at beside each price
- [ ] T010 Client tests

## Seed, contract, docs

- [ ] T011 Seed: a few compare-at prices, checked
- [ ] T012 Bruno for both endpoints, the refusals and the filter; `docs/reference` regenerated
- [ ] T013 On the stack: the quickstart and a browser check
- [ ] T014 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T015 Merged, closes #369

## Evidence

(Filled in when the work is verified.)
