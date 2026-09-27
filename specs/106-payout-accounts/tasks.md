---
description: "Task list for Sellers say where their payouts go"
---

# Tasks: Sellers say where their payouts go

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Identity (US1)

- [x] T001 [US1] `SellerPayoutAccount`, configuration, migration; `EmailTemplate.PayoutAccountChanged` and its words
- [x] T002 [US1] `GetMyPayoutAccountQuery` (masked), `SetMyPayoutAccountCommand` (validator, audit masked, email), `GetPayoutAccountsQuery` (Admin); routes
- [x] T003 [US2] `payout_accounts.proto`; `PayoutAccountsService` (Admin only), mapped
- [x] T004 `PayoutAccountTests`

## Phase 2: Order (US2)

- [x] T005 [US2] `IPayoutAccounts` / `GrpcPayoutAccounts`; `payouts` destination columns and migration
- [x] T006 [US2] `RecordPayout` refuses without an account and freezes the destination; responses; the test fixture's fake; `PayoutTests`

## Phase 3: Storefront

- [x] T007 Payout-account card on `/shop/payouts`; accounts, "no payout account" and "changed recently" on `/admin/payouts`; the destination on payout lists; words en/vi; tests

## Phase 4: Verification and docs

- [x] T008 Mutations (quickstart Scenario 3), each red; full Identity, Order and client suites
- [x] T009 [P] Bruno; rebuilt Identity, Order and the storefront; Mailpit shows the email; post-design Constitution re-check
- [x] T010 Docs: marketplace, email, service-to-service, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T011 Merged as #226, closing #213

## Evidence

- Identity: `PayoutAccountTests` 4/4 and the email tests 24/24 (twelve emails in two languages); Order 304/304;
  client 536/536, `oxlint` and `tsc` clean.
- Mutations, each red then restored: the handler not masking the audit snapshot, the email carrying the whole
  number, the validator's pattern loosened, the gRPC service open to any role, `RecordPayout` recording without an
  account, the claim not writing the destination, the admin page letting Pay through without an account and not
  marking a recent change, the shop page not showing the masked number.
- Bruno through the rebuilt containers: 326/326 requests, 525/525 tests - the seller gives an account before the
  payout checks, reads it masked, the administrator reads it whole, a customer is refused both.
- Mailpit: two `PayoutAccountChanged` emails ("Tài khoản nhận tiền của bạn vừa được thay đổi"); the body carries the
  last four digits and not the account number.
- Post-design Constitution re-check: see [plan.md](plan.md) - no violation found.
