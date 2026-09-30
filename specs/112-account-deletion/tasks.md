---
description: "Task list for A person deletes their account"
---

# Tasks: A person deletes their account

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared

- [x] T001 `AccountDeleted` in Contracts; `account_standing.proto`; `PersonalDataInventory.Kept`; `ConflictException` facts

## Phase 2: Order's standing (US2)

- [x] T002 Order serves gRPC on a second port; `AccountStanding.GetMyStanding` from the token; `AccountStandingTests` per blocker
- [x] T003 Compose, `start-dev`, CI: Order's gRPC port; Identity's `Services:Order` address

## Phase 3: Identity deletes (US1, US2, US4)

- [x] T004 `users.DeletedAt` migration; `DeleteAccountCommand` (password, staff, standing, erase, publish, audit); `DELETE /api/auth/me`; gateway route with the `sign-in` limiter
- [x] T005 `AccountDeletionTests` in Identity: the row, the rows, the messages, re-registration, the old password, a wrong password, staff, each blocker, Order down

## Phase 4: The erasures (US1, US3)

- [x] T006 [P] Catalog `EraseAccountFromCatalogConsumer` + test (reviews, questions, saved, eligibility, reports, a seller's shop)
- [x] T007 [P] Order `EraseAccountFromOrdersConsumer` + test (delivery copies, return reasons, vouchers, payouts)
- [x] T008 [P] Cart `EraseAccountFromCartConsumer` + test
- [x] T009 [P] Activity `EraseAccountFromActivityConsumer` + test (notices, actor email, summaries, snapshots)
- [x] T010 [P] Payment: `Kept` declared, test that nothing personal remains

## Phase 5: Storefront

- [x] T011 "Delete my account" on `/account`: what goes, what stays, password, confirmation; refusal reasons worded; sign out on success; "a former customer" on reviews and questions; tests

## Phase 6: Verification and docs

- [x] T012 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [x] T013 Bruno (`my-data/`, a 401 in `security-checks`); rebuilt containers; the post-design Constitution re-check
- [x] T014 Docs: personal-data page (deletion), CLAUDE.md, service-to-service doc (the new edge), backlog, timeline; `generate_reference.py`
- [x] T015 Merged as #232, closes #217

## Evidence (2026-10-01)

- **Server**: every suite green against PostgreSQL - Activity 43, ApiGateway 14, Cart 22, Catalog 261, Identity 243,
  Inventory 78, Orchestrator 18, Order 327, Payment 31. New: `AccountDeletionTests` (7, Identity), `AccountStandingTests`
  (16 cases, Order), an erasure test beside each service's `MyDataTests` (each also re-sends the erasure: a redelivery
  changes nothing), a 409's facts in `ForbiddenProblemTests`, and the gateway's sign-in allowance on `DELETE /api/auth/me`.
- **Client**: 589/589 (101 files), oxlint and `tsc -b` clean - the refusal worded per reason, the confirmation before
  anything is sent, a wrong password on its field, and "a former customer" on a review without a name.
- **Mutations, 24, every one red by a test**: Identity - addresses left, the email kept, `AccessTokensRevoked` not
  published, blockers ignored, staff not refused, a wrong password not counted, the email in the audit entry, a
  deleted profile still readable; Order - open orders for everybody, a delivered parcel still open, paid-out earnings
  still unpaid, a sent-back return not open, the street kept, the payout holder kept; Catalog - saved products kept,
  the author's name kept, the shop left open, `reviews` dropped from `Kept`; Activity - the email left in summaries,
  notices kept, snapshots kept; Cart - checkout outcomes kept; storefront - a refusal's reasons ignored, the
  confirmation skipped.
- **Bruno** against rebuilt Identity, Catalog, Order (now with 6059 published), Cart, Payment, Activity and the
  gateway: 376/376 requests, 609/609 tests. In the containers Identity asked Order over gRPC: a wrong password 400, the
  deletion 204, the old token 401, signing in 401, the email registering again 200; without a token 401. RabbitMQ shows
  the four erase queues with one consumer each.
- **Decided while building**: the deletion requests live at the end of Bruno's `my-data` folder rather than a folder of
  their own (no renumbering); signing in with a deleted account's old email is 401, as any wrong password is (the
  quickstart said 400); `GET /api/auth/me` answers 404 for a deleted account even before the revocation arrives.
