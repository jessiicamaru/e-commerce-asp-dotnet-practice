---
description: "Task list for A voucher's terms can be corrected"
---

# Tasks: A voucher's terms can be corrected

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2, US3)

- [x] T001 `EditVoucherCommand` + validator; `IVoucherRepository.TryEditAsync` - the guarded statement, the amounts and the condition, the audit stage, one transaction
- [x] T002 `PUT /api/vouchers/{id}`
- [x] T003 `VoucherEditingTests`: extended end date and an earlier order untouched; limit under uses 409; minimum lowered qualifies a checkout; disabled 409; other seller 404; administrator on a seller's 404; audit before/after; an edit racing claims

## Phase 2: Storefront (US1)

- [x] T004 "Edit" on an active voucher in the shared voucher page: a dialog prefilled with the terms; words; tests

## Phase 3: Verification and docs

- [x] T005 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [x] T006 Bruno (edit, 409, 403); rebuilt Order container; the post-design Constitution re-check
- [x] T007 Docs: vouchers page, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T008 Merged as #233, closes #219

## Evidence (2026-10-01)

- **Server**: Order 336/336 and ApiGateway green against PostgreSQL; `VoucherEditingTests` (9): an extended end applies
  and an earlier order's redemption is untouched; a total below the uses is 409 - in the handler and, called directly,
  in the statement on its own; a lowered minimum lets a checkout qualify through `CheckoutPricing`; a minimum quantity
  only where there is one; disabled 409; a currency it does not have 400; an end in the past or before the start 400;
  another seller's and (to an administrator) a seller's voucher 404; the audit entry's before and after; an edit racing
  six checkouts over five rounds never leaves more uses than the limit.
- **Client**: 596/596, oxlint and `tsc -b` clean - prefilled terms in local time, every term sent as its new value, no
  minimum quantity where there is none, a refusal kept in the open dialog, no Edit on a disabled voucher.
- **Mutations, 14, every one red**: the statement's limit guard (first survived - the race had not overlapped, so the
  statement is now tested on its own), the limit checked nowhere, the owner checked nowhere, an administrator on any
  voucher, the status checked nowhere, the audit not staged, the minimum subtotal, the minimum quantity and the end not
  written, a past end accepted (first survived - the test's end was also before the start), an end before the start
  accepted, a new currency accepted; in the storefront a minimum quantity sent regardless, the prefill lost.
- **Bruno** against the rebuilt Order container: 380/380 requests, 614/614 tests - corrected (200), an end in the past
  (400), a customer (403), a disabled voucher (409). The `seller` folder's requests from the disable on moved up by 3-4.
- **Post-design Constitution re-check**: unchanged - Order's own table, the guard in one statement with the audit entry
  in its transaction, the owner from the token.
