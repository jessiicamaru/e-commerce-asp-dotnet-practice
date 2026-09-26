---
description: "Task list for Shop applications"
---

# Tasks: Shop applications

> Completed on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Input**: Design documents from `/specs/044-shop-applications/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The guarantees - one pending application per person, a decision applied once - belong
to the database, so constitution Principle V requires them to be tested against a real PostgreSQL.

**Organization**: By user story, after the shared data work. **About the numbering**: T001-T007 are the
seven tasks of the original record, kept with their wording and given file paths; T008 onwards were added
on 2026-09-27 to spell out what those seven covered. IDs therefore do not run in phase order.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (somebody asks to sell), US2 (staff decide), US3 (an approved applicant reaches their shop)

## Path Conventions

`ID` below abbreviates `server/src/Services/Identity/Ecommerce.Identity`, so `ID.Application/` is
`server/src/Services/Identity/Ecommerce.Identity.Application/`.

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: The table, the repository and the notification kinds every story needs.

- [X] T001 Identity: `ShopApplication` entity, configuration (one-pending index), migration - `ID.Domain/Entities/ShopApplication.cs` (entity plus `ShopApplicationStatus`), `ID.Infrastructure/Configurations/ShopApplicationConfiguration.cs` (`shop_applications`, `IX_shop_applications_one_pending` unique on `UserId` filtered `"Status" = 'Pending'`, index on `(Status, CreatedAt)`, FK to `users` with cascade), `ID.Infrastructure/Migrations/20260923204529_AddShopApplications.cs`
- [X] T008 [P] Declare `IShopApplicationRepository` and the `ShopApplicationRow` record (application plus applicant email and names) in `ID.Application/Common/Interfaces/IShopApplicationRepository.cs`, with `TryDecideAsync(id, decision, reason, decidedBy, decidedAt, stage)` documented as "only the winner's stage runs"
- [X] T009 Implement `ShopApplicationRepository` in `ID.Infrastructure/Persistence/Repositories/ShopApplicationRepository.cs`: `HasPendingAsync`, `GetMineAsync` (newest first), `GetAsync` and `GetPageAsync` joined to `users` and ordered after the join (pending oldest first, the rest newest first), and `TryDecideAsync` - change tracker cleared, a transaction inside `CreateExecutionStrategy().ExecuteAsync`, the guarded `ExecuteUpdateAsync ... WHERE Status == Pending`, then the stage, one `SaveChangesAsync`, commit
- [X] T010 Add `DbSet<ShopApplication>` to `ID.Infrastructure/Persistence/ApplicationDbContext.cs` and register the repository in `ID.Infrastructure/DependencyInjection.cs`
- [X] T011 [P] Add `NotificationKind.ShopApproved` and `ShopRejected` to `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`

**Checkpoint**: the migration applies to `ecommerce_identity_db` and the solution builds.

---

## Phase 2: User Story 1 - Somebody asks to sell (P1)

**Goal**: Nobody becomes a seller by asking; the asking is recorded and visible to the asker.

**Independent test**: Register through `register-seller`; roles are `["Customer"]`, one application is
pending, `/api/sellers/me` is 403 (quickstart scenarios 1 and 2).

- [X] T002 [US1] Identity: `register-seller` creates a customer and a pending application - `ID.Application/Auth/Commands/RegisterSeller/RegisterSellerCommand.cs`: `Customer` role only, a `ShopApplication` instead of a `SellerProfile`, no `SellerRegisteredEvent` (the `IPublishEndpoint` dependency removed), audit action `ShopApplied` on subject `ShopApplication`, optional `Description` (1000) and `Phone` (20), and one `SaveChangesAsync` for the account, the application, the audit entry and the session where there had been two
- [X] T012 [US1] `ApplyForShopCommand` and its validator (shop name required, at most 100; description 1000; phone 20) and `ShopApplicationRules.EnsureMayApplyAsync` (already a seller → 409, already waiting → 409) in `ID.Application/ShopApplications/ShopApplicationFeatures.cs`; the handler takes the applicant from `ICurrentUser`, stores blanks as null, records `ShopApplied`, saves once, and turns a violation of `IX_shop_applications_one_pending` into the same 409
- [X] T013 [US1] `GetMyShopApplicationsQuery` and `ShopApplicationResponse.Mine` (applicant email and name left null) in `ID.Application/ShopApplications/ShopApplicationFeatures.cs`

---

## Phase 3: User Story 2 - Staff decide (P1)

**Goal**: A moderator or an administrator approves or rejects, exactly once, with everything the decision
causes committed with it.

**Independent test**: Approve as staff, approve again, sign in as the applicant (quickstart scenario 4).

- [X] T014 [US2] `GetShopApplicationsQuery` with its validator (`page > 0`, `pageSize` 1-50, status `Pending`/`Approved`/`Rejected` case-insensitive or absent) and `ShopApplicationResponse.ForStaff` in `ID.Application/ShopApplications/ShopApplicationFeatures.cs`
- [X] T015 [US2] `ApproveShopApplicationCommand` handler in `ID.Application/ShopApplications/ShopApplicationFeatures.cs`: 404 when unknown, then `TryDecideAsync` with a stage that adds `Seller` (if not held), inserts the `SellerProfile` under the application's shop name, publishes `SellerRegisteredEvent`, records `ShopApproved` under Moderation and notifies `ShopApproved` (data `shop`, link `/shop`)
- [X] T016 [US2] `RejectShopApplicationCommand` with its validator (reason required, at most 500) and handler in `ID.Application/ShopApplications/ShopApplicationFeatures.cs`: stage records `ShopRejected` under Moderation and notifies `ShopRejected` (data `shop`, `reason`, link `/open-shop`)
- [X] T017 [US2] `DecidedAsync` in the same file: a decision that changed no row re-reads the application and throws `ConflictException("This application is already approved.")` (or `rejected`)
- [X] T003 [US1] [US2] Identity: apply / mine / queue / approve / reject, guarded decision with a stage; controller - `ID.WebApi/Controllers/ShopApplicationsController.cs` (`api/shop-applications`, `[Authorize]`; `POST` is `Customer` and answers 201; `GET mine` any signed-in caller; `GET`, `POST {id}/approve`, `POST {id}/reject` are `StaffRoles.Staff`)

**Checkpoint**: an approval makes a seller; a second is 409.

---

## Phase 4: Tests

- [X] T004 Tests: `ShopApplicationTests` (no shop before approval, approval does it all once, five racing approvals, reject then re-apply, rules, queue order); seller tests go through approval - `server/tests/Ecommerce.Identity.Tests/ShopApplicationTests.cs`, six tests against PostgreSQL on 5435 with MassTransit's test harness collecting `SellerRegisteredEvent`, `UserNotificationRequested` and `AuditEntryRecorded`
- [X] T018 [P] Register `IShopApplicationRepository` in the test container and add `ApprovedSellerAsync` (register, read the application, approve as a moderator) to `server/tests/Ecommerce.Identity.Tests/IdentityTestFixture.cs`
- [X] T019 [P] Rewrite `server/tests/Ecommerce.Identity.Tests/SellerRolesTests.cs`: registering to sell is a customer with a pending application; an approved seller holds `Seller` and `Customer`; refresh keeps the roles - all through `ApprovedSellerAsync`
- [X] T020 [P] Update `server/tests/Ecommerce.Identity.Tests/AuditTests.cs`: registration records `ShopApplied`; the rename test uses an approved seller

---

## Phase 5: Gateway and Bruno

- [X] T005 Gateway route; Bruno seller folder applies, is refused, is approved, signs in as a seller; 401 without a token
- [X] T021 [P] Add `shop-applications-route` (`/api/shop-applications/{**catch-all}`) and `shop-applications-root-route` (`/api/shop-applications`) on `identity-cluster` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`
- [X] T022 [P] In `bruno/seller/`: `register a seller.yml` now asserts roles `["Customer"]`; add `the applicant waits for review.yml` (sets `applicationId`), `an applicant has no shop yet.yml` (403), `a customer cannot approve a shop.yml` (403), `staff see the application in the queue.yml`, `an administrator approves the shop.yml`, `approving it again is 409.yml`, `the seller signs in again as a seller.yml` (resets `sellerToken`); renumber the folder's existing requests after them
- [X] T023 [P] Add `bruno/security-checks/applying to sell without a token is 401.yml`

---

## Phase 6: User Story 3 - An approved applicant reaches their shop (P2), and the storefront

**Goal**: Applying, deciding and arriving at the shop all work from the storefront.

**Independent test**: quickstart scenario 8.

- [X] T006 [US3] Client: `/open-shop`, `/admin/shops`, menu entry, session refresh, wording; tests
- [X] T024 [P] [US1] `client/src/services/shop-applications/index.ts` (the `ShopApplications` class: `mine`, `apply`, `list`, `approve`, `reject`) and `client/src/services/shop-applications/types.ts`
- [X] T025 [P] [US1] `client/src/hooks/shop-applications/index.ts` (`useMyShopApplications`, `useApplyForShop`, `useShopApplications`, `useDecideShopApplication` - re-reads every list after a decision) and the two keys in `client/src/constants/query-keys/index.ts`
- [X] T026 [US3] `refreshSession()` on `AuthState` in `client/src/context/auth/types.ts`, wired to the existing single-flight `refresh` in `client/src/context/auth/index.tsx`
- [X] T027 [US1] [US3] `client/src/pages/open-shop/index.tsx`: the history with status and reason, the form only when nothing waits and nothing is approved, "Go to my shop" renewing the session before navigating; tests in `client/src/pages/open-shop/index.test.tsx` (sends what was typed; pending offers no second form; rejected shows why and allows again; approved renews and navigates; a server refusal shown in its words)
- [X] T028 [US2] `client/src/pages/admin-shops/index.tsx`: a tab per status in the address, applicant name and email, approve at a press, reject only with a reason in a dialog, paged at `PAGE_SIZE`; tests in `client/src/pages/admin-shops/index.test.tsx` (waiting first; approve; reject needs a reason; decided tab has no buttons; somebody else deciding first shown as the server says)
- [X] T029 [P] [US3] "Open a shop" for anybody who does not sell in `client/src/components/layout/user-menu/index.tsx`; the `/open-shop` route (signed in) and `/admin/shops` in `client/src/routes/index.tsx`; the "Shop applications" link for staff in `client/src/layouts/admin-layout/index.tsx`; a moderator's console opening on `/admin/shops` in `client/src/pages/admin-home/index.tsx`; the admin layout test now expects the link and a moderator landing on the shops page (`client/src/layouts/admin-layout/index.test.tsx`); test doubles of `AuthState` gain `refreshSession` (`client/src/test/render.tsx`, `client/src/components/auth/require-role/index.test.tsx`, `client/src/pages/product/index.test.tsx`, `client/src/pages/shop-sales/index.test.tsx`), and `render.tsx` gains `renderAsCustomer`
- [X] T030 [P] Wording in `client/src/locales/{en,vi}/seller.json` (`apply.*`, `openShop`), `admin.json` (`shops.*`, `menu.shops`) and `notifications.json` (`ShopApproved`, `ShopRejected`)

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T007 Run everything; verify-saga; docs; local demo seeder approves its sellers
- [X] T031 Update `CLAUDE.md`: the Identity row of the service map, the "A shop is an application first" paragraph, and the Identity test count (71). This was the pull request's only documentation change; `docs/features/marketplace.md` describes the feature from the documentation rewrite of 2026-09-24 (`f466a63`)
- [X] T032 Mutation checks, each red and then restored (from the pull request): remove the `Status = 'Pending'` guard → the approval test and the race test fail; show the form while an application waits → the page test fails
- [X] T033 Screenshots of `/admin/shops` at 1360px and at 390px with no horizontal overflow (from the pull request)
- [X] T034 Merge PR #96, "feat(identity): a new shop waits for a moderator or an administrator to approve it" (closes #89), on 2026-09-23 as `8e5259d`

---

## Dependencies & Execution Order

- **Phase 1** blocks everything: the table and the repository.
- **US1 (Phase 2)** needs Phase 1. **US2 (Phase 3)** needs Phase 1 and an application to decide, so it
  follows US1 in practice; T003 exposes both.
- **Tests (Phase 4)**: T018 before T004, T019 and T020, which all use `ApprovedSellerAsync`.
- **Phase 5** needs T003 and the gateway route; T022 depends on T021.
- **Phase 6**: T024 → T025 → T027, T028; T026 before T027. Needs the endpoints for manual checks only -
  the page tests mock the service class.
- **Phase 7** last.

### Parallel opportunities

- T008 and T011 - different projects.
- T018, T019, T020 - separate test files.
- T021, T022, T023 - gateway and two Bruno folders.
- T024, T025, T029, T030 - separate client files.

## Notes

- 34 tasks: 7 from the original record, 27 added in the backfill.
- Evidence recorded at the merge (PR #96): Identity tests 71/71, client 205/205 with lint, type-check and
  build clean, Bruno 156/156 requests and 249 tests, `verify-saga.sh` passing.
- `local/seed-demo.py` is gitignored; the pull request records that it approves its demo sellers now.
