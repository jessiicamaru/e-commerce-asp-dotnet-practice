---
description: "Task list for Email delivery"
---

# Tasks: Email delivery

> Completed on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Input**: Design documents from `/specs/087-email-delivery/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included - an authorization boundary (Admin only) and a guarded transition (retry once).

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 see failures and why, US2 send one again, US3 never a dead link

The original four tasks are kept in full: original T001 is T001-T004, T002 is T005, T003 is T006-T008, T004 is
T009-T011.

---

## Phase 1: Identity

- [X] T001 [US1] [US2] [US3] `EmailDelivery` (the query, the retry, `MayRetry`, `OutgoingEmailResponse` with no data field, the validator) in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailDelivery.cs`
- [X] T002 [US1] [US2] `IOutgoingEmailRepository`'s `PageAsync`, `GetWithRecipientAsync` and `TryRetryAsync` in `.../Application/Email/EmailInterfaces.cs`, implemented in `.../Infrastructure/Email/OutgoingEmailRepository.cs` (left join to `users`; the guarded `ExecuteUpdateAsync`)
- [X] T003 [US1] [US2] `EmailsController` (`Admin`) in `.../Identity.WebApi/Controllers/EmailsController.cs`
- [X] T004 [P] [US1] The gateway routes for `/api/emails` (`emails-root-route`, `emails-route`) in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`

## Phase 2: Tests

- [X] T005 `EmailDeliveryTests` (4) in `server/tests/Ecommerce.Identity.Tests/EmailDeliveryTests.cs`:
  - listed with why and whom, never the data;
  - states and search;
  - a retry goes out once, is audited, and a second is 409;
  - a reset link is never retried, and an unknown id is 404.

## Phase 3: Storefront

- [X] T006 [P] [US1] [US2] `services/outgoing-email` (`index.ts`, `types.ts`), `hooks/outgoing-email` and `pages/admin-email-delivery` under `client/src/`
- [X] T007 [US1] The route (`client/src/routes/index.tsx`, `RequireRole` Admin), the nav entry (`client/src/layouts/admin-layout/index.tsx`, admin only), the query key (`client/src/constants/query-keys/index.ts`), and the words in both languages (`client/src/locales/{en,vi}/admin.json`)
- [X] T008 [US1] [US2] [US3] Tests for the service and the page: `client/src/services/outgoing-email/index.test.ts`, `client/src/pages/admin-email-delivery/index.test.tsx`

## Phase 4: Verification and docs

- [X] T009 Three mutations (no guard, a reset link retried, the data returned), each turning `EmailDeliveryTests` red
- [X] T010 [P] Bruno `admin-users/` 31-33 and a 401 in `security-checks/`, and the docs: email, CLAUDE.md, decisions, reference, counts, timeline and backlog
- [X] T011 Merged as #179 on 2026-09-26 UTC (closes #175), after Identity 174/174, client 466/466 with lint and type-check, and Bruno 272/272 requests and 444/444 tests

---

## Dependencies & Execution Order

- T002 before T001's handlers can run; T003 after T001; T004 independent.
- T005 against T001-T003.
- The storefront (T006-T008) needs only the contract; it was built after the server in the same branch.
- Phase 4 last.
