---
description: "Task list for A seller edits what they listed"
---

# Tasks: A seller edits what they listed

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [x] T001 `UpdateProductDetailsCommand` + validator + `PUT /api/products/{id}`
- [x] T002 `original` and `translations` on the lookup; the seller's page reads signed in
- [x] T003 Catalog tests: owner, other seller 404, admin, review move, category, nothing changed, response

## Phase 2: Storefront (US1-US4)

- [x] T004 Details card
- [x] T005 Translations card
- [x] T006 Add-variant form
- [x] T007 New-product form: submit at the end, honest hint
- [x] T008 Vitest for each

## Phase 3: Verification and docs

- [x] T009 Mutations, each red
- [x] T010 Bruno requests; rebuilt Catalog; the page in a browser
- [x] T011 Catalog/marketplace docs, CLAUDE.md, generate_reference.py, audit label, timeline, backlog
- [x] T012 Merged, closes #240 - #263

## Evidence

**2026-10-01**

- **Catalog** (PostgreSQL): `ProductDetailsTests` (7) - a seller's edit changes the three fields and sends an approved
  product back to review (`ProductDetailsEdited` and `ProductSentForReview` audited); a category change alone does too;
  the same values change nothing; another seller is 404 and nothing changes; an administrator's edit stays approved;
  an unknown category (400 on `CategoryId`) and an empty name write nothing; the lookup carries the original and each
  language's text as stored, a list neither. Catalog 268/268.
- **Mutations, each red**: no review move (2); no ownership check; no category check; "changed" always true; no stored
  text on the lookup; on the client, the details read from the browsing language, the translations from the fallback,
  the price sent as text, and the page reading anonymously (18 red).
- **Vitest** 634/634 - including the audit-label test, which required words for `ProductDetailsEdited`.
- **Bruno** against the rebuilt Catalog: four requests in `seller` - 390/390 requests, 632 tests.
- **Browser** (Edge, Vite dev): a seller opened a product waiting for review (a 404 before: the page read anonymously),
  saved the details, saved English, and added a `Colour: Silver` variant (201). Two things were found there: the toast
  lost to the keyed card's remount (now `mutateAsync`), and the server's 409 for a second variant with no options on a
  product sold one way (the form now starts such a product with one empty option and says why).
- **Post-design Constitution re-check**: unchanged.
