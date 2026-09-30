---
description: "Task list for A person deletes their account"
---

# Tasks: A person deletes their account

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared

- [ ] T001 `AccountDeleted` in Contracts; `account_standing.proto`; `PersonalDataInventory.Kept`; `ConflictException` facts

## Phase 2: Order's standing (US2)

- [ ] T002 Order serves gRPC on a second port; `AccountStanding.GetMyStanding` from the token; `AccountStandingTests` per blocker
- [ ] T003 Compose, `start-dev`, CI: Order's gRPC port; Identity's `Services:Order` address

## Phase 3: Identity deletes (US1, US2, US4)

- [ ] T004 `users.DeletedAt` migration; `DeleteAccountCommand` (password, staff, standing, erase, publish, audit); `DELETE /api/auth/me`; gateway route with the `sign-in` limiter
- [ ] T005 `AccountDeletionTests` in Identity: the row, the rows, the messages, re-registration, the old password, a wrong password, staff, each blocker, Order down

## Phase 4: The erasures (US1, US3)

- [ ] T006 [P] Catalog `EraseAccountFromCatalogConsumer` + test (reviews, questions, saved, eligibility, reports, a seller's shop)
- [ ] T007 [P] Order `EraseAccountFromOrdersConsumer` + test (delivery copies, return reasons, vouchers, payouts)
- [ ] T008 [P] Cart `EraseAccountFromCartConsumer` + test
- [ ] T009 [P] Activity `EraseAccountFromActivityConsumer` + test (notices, actor email, summaries, snapshots)
- [ ] T010 [P] Payment: `Kept` declared, test that nothing personal remains

## Phase 5: Storefront

- [ ] T011 "Delete my account" on `/account`: what goes, what stays, password, confirmation; refusal reasons worded; sign out on success; "a former customer" on reviews and questions; tests

## Phase 6: Verification and docs

- [ ] T012 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [ ] T013 Bruno (`account-deletion/`, a 401 in `security-checks`); rebuilt containers; the post-design Constitution re-check
- [ ] T014 Docs: personal-data page (deletion), CLAUDE.md, service-to-service doc (the new edge), backlog, timeline; `generate_reference.py`
- [ ] T015 Merged as #232, closes #217
