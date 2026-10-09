---
description: "Task list for A compare-at price per variant"
---

# Tasks: A compare-at price per variant

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 The two columns, their CHECKs and the `AddCompareAtPrices` migration (expand-only)
- [x] T002 Set and clear a compare-at: currency, representable, above the price, ownership, audit, no review
- [x] T003 The price writers clear a compare-at they reach (research D2)
- [x] T004 `compareAtPrice` on the variant and the product (research D4); `onSale` on the listing
- [x] T005 `CompareAtPriceTests` on PostgreSQL, incl. the CHECK, the clearing, the filter and checkout's price; a mutation

## Client

- [x] T006 Types, service, hooks; locales (incl. audit labels)
- [x] T007 The struck-through price and the percentage on cards and the product page
- [x] T008 The "On sale" filter
- [x] T009 The seller's compare-at beside each price
- [x] T010 Client tests

## Seed, contract, docs

- [x] T011 Seed: a few compare-at prices, checked
- [x] T012 Bruno for both endpoints, the refusals and the filter; `docs/reference` regenerated
- [x] T013 On the stack: the quickstart and a browser check
- [x] T014 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T015 Merged, closes #369

## Evidence

Verified on 2026-10-09 against the compose stack rebuilt from this branch:

- **Server tests**: `Ecommerce.Catalog.Tests` 344 passed, 0 failed, on PostgreSQL - 14 in `CompareAtPriceTests`: read
  back on the variant and the card and never converted; the card's compare-at is the one of the variant giving its
  price; five refusals (equal, below, a fraction of a dong, a currency with no price, a currency the shop does not
  price in) changing nothing; the database's CHECK refusing a direct write; a lowered price keeping it and a raised
  one clearing it in both currencies and through both endpoints; quiet clearing; `onSale` per currency, and not for a
  reduced dearer shape alone; checkout's `PriceVariants` charging the price; a seller without review, another seller's
  404.
- **Mutations**: without `CompareAt.ClearIfNotBelow` on a `variant_prices` row, the raised-price test fails (the CHECK
  refuses the save); with the `onSale` filter switched off, the filter test fails; with the filter back to "any reduced
  variant", the dearer-shape test fails. All restored.
- **Bruno found a design flaw** (first run of the 8 new requests at the end of `product/`, seq 82-89): the requests
  reduced the product's first variant, which was not its cheapest, so the card rightly showed nothing struck through -
  while `onSale` still listed it. A shopper filtering "On sale" would have found a card with no reduction. `onSale` now
  means what the card shows (research D6), with a test, and the request reduces the cheapest shape. Second run: 722 of
  723 tests; the one failure is `my-data/payment`, filed as #364.
- **Client**: 144 files, 793 tests passed - the percentage (rounded down, nothing under one percent), the struck-through
  price, the On sale filter in the address, the seller's compare-at saved after the price (shown by mutation: sent
  first, the test fails) and cleared from an emptied box. Lint clean: both only-export-components warnings removed by
  moving `percentOff` and `parseOptionLines` (from #366) into files of their own. Both apps build.
- **Migration**: `AddCompareAtPrices` applied at the container's start - two nullable columns and two CHECKs.
- **Seed**: `seed/catalogue.py` checks `compareAtVnd`/`compareAtUsd` (refusing one below its price, shown on a copy);
  four products carry one; seeded through the API, `onSale` lists exactly those four in dong and in dollars.
- **In a browser**: "On sale" ticked lists the 4, each card struck through with -19%, -20%, -21%; the Sony WH-1000XM5
  page shows ~~9.990.000₫~~ 7.990.000₫ -20%, and of its two shapes only the reduced one (Black) is struck through.
  No CSP error (the one console line is the signed-out session's refresh, 401).
- **Not checked in a browser**: the seller's compare-at boxes (a seller account with a confirmed address); covered by
  the seller page's tests.
- **Docs**: `docs/reference` regenerated (224 endpoints, 65 tables).
