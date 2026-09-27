---
description: "Task list for Sellers say where their payouts go"
---

# Tasks: Sellers say where their payouts go

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Identity (US1)

- [ ] T001 [US1] `SellerPayoutAccount`, configuration, migration; `EmailTemplate.PayoutAccountChanged` and its words
- [ ] T002 [US1] `GetMyPayoutAccountQuery` (masked), `SetMyPayoutAccountCommand` (validator, audit masked, email), `GetPayoutAccountsQuery` (Admin); routes
- [ ] T003 [US2] `payout_accounts.proto`; `PayoutAccountsService` (Admin only), mapped
- [ ] T004 `PayoutAccountTests`

## Phase 2: Order (US2)

- [ ] T005 [US2] `IPayoutAccounts` / `GrpcPayoutAccounts`; `payouts` destination columns and migration
- [ ] T006 [US2] `RecordPayout` refuses without an account and freezes the destination; responses; the test fixture's fake; `PayoutTests`

## Phase 3: Storefront

- [ ] T007 Payout-account card on `/shop/payouts`; accounts, "no payout account" and "changed recently" on `/admin/payouts`; the destination on payout lists; words en/vi; tests

## Phase 4: Verification and docs

- [ ] T008 Mutations (quickstart Scenario 3), each red; full Identity, Order and client suites
- [ ] T009 [P] Bruno; rebuilt Identity, Order and the storefront; Mailpit shows the email; post-design Constitution re-check
- [ ] T010 Docs: marketplace, email, service-to-service, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T011 Merged as #226, closing #213
