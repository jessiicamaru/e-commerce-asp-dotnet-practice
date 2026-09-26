---
description: "Task list for Audit gaps and the misleading reuse warning"
---

# Tasks: Audit gaps and the misleading reuse warning

> Completed on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, and written first: each missing entry and the stale-tab harm had a test that failed before
the change (constitution Principle V).

## Format: `[ID] [P?] [Story] Description`

The first four tasks are the list as written on 2026-09-24, kept verbatim. T005 onwards break them down and add
what the pull request also did.

- [X] T001 [US1] [US2] Tests first: `server/tests/Ecommerce.Catalog.Tests/AuditTests.cs` (category translation set/remove, product translation removed), `server/tests/Ecommerce.Identity.Tests/AuditTests.cs` (sign-out, default address, reuse), `server/tests/Ecommerce.Identity.Tests/RefreshTokenReuseTests.cs` (a stale tab after an unlock)
- [X] T002 [US1] Catalog handlers: `Categories/Translations/SetCategoryTranslationCommand.cs`, `Products/Translations/SetProductTranslationCommand.cs`
- [X] T003 [US1] [US2] Identity handlers: `Addresses/Commands/SetDefaultAddress/`, `Auth/Commands/Logout/`, `Auth/Commands/Refresh/`
- [X] T004 Mutation checks; docs `docs/features/audit-and-notifications.md`, `docs/features/auth/*`, `docs/project/*`

---

## Phase 1: Tests first

- [X] T005 [P] [US1] Add `Translating_a_category_and_removing_translations_are_recorded` to `server/tests/Ecommerce.Catalog.Tests/AuditTests.cs`: set and remove a category's English text, remove a product's; assert the two category actions in order and a `ProductTranslationRemoved` under Catalog whose "before" holds the removed text
- [X] T006 [P] [US1] Add `Signing_out_is_recorded_as_the_person_and_nothing_is_recorded_for_nothing` to `server/tests/Ecommerce.Identity.Tests/AuditTests.cs`: one `SignedOut` under Security with the person as actor and no token in it; an unknown token records nothing
- [X] T007 [P] [US1] Add `Changing_the_default_address_is_recorded_without_the_address` to `server/tests/Ecommerce.Identity.Tests/AuditTests.cs`: one `DefaultAddressChanged` under User containing no street
- [X] T008 [P] [US1] Add `Detected_reuse_is_on_the_record` to `server/tests/Ecommerce.Identity.Tests/AuditTests.cs`, with `SendAsync` and non-generic `RecordedAsync` helpers for void commands
- [X] T009 [P] Add `AgeRevocationAsync` to `server/tests/Ecommerce.Identity.Tests/IdentityTestFixture.cs`, moving a rotation five minutes into the past, beyond the grace window
- [X] T010 [P] [US2] Add `A_stale_tab_from_before_a_lock_does_not_end_the_session_after_the_unlock` to `server/tests/Ecommerce.Identity.Tests/RefreshTokenReuseTests.cs`: lock, unlock, sign in again, present the old token (401), and the new session still refreshes

## Phase 2: User Story 1 - every write that matters is on the record (P1)

- [X] T011 [US1] Inject `IAuditTrail` into `SetCategoryTranslationCommandHandler` and `RemoveCategoryTranslationCommandHandler` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Translations/SetCategoryTranslationCommand.cs`; record `CategoryTranslated` (before/after) and `CategoryTranslationRemoved` (before) ahead of the one save
- [X] T012 [US1] Record `ProductTranslationRemoved` in `RemoveProductTranslationCommandHandler` (`server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Translations/SetProductTranslationCommand.cs`) before `ProductReview.AfterSellerEditAsync`
- [X] T013 [US1] Record `DefaultAddressChanged` with no snapshot in `server/src/Services/Identity/Ecommerce.Identity.Application/Addresses/Commands/SetDefaultAddress/SetDefaultAddressCommandHandler.cs`, inside the transaction and before the promoting save
- [X] T014 [US1] Record `SignedOut` with `actor: AuditActors.Of(user)` in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Logout/LogoutCommandHandler.cs`, only when a session was found and removed

## Phase 3: User Story 2 - a session ended by a stop is not called theft (P1)

- [X] T015 [US2] In `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Refresh/RefreshTokenCommandHandler.cs`, define reuse as `ReplacedByToken is not null && now - revokedAt > ReuseGrace`; record `SessionReuseDetected` and save it before `RevokeAllRefreshTokensAsync`; log an unrotated revoked token at Information and revoke nothing; keep the one 401 for every case

## Phase 4: Polish

- [X] T016 Run the three mutations from the pull request - an unrotated revoked token counts as reuse again; sign-out not recorded; category translation not recorded - and confirm each turns a test red, then restore
- [X] T017 Run the Identity (83/83) and Catalog (161/161) suites against real PostgreSQL
- [X] T018 [P] Update `docs/features/audit-and-notifications.md` (the action table), `docs/features/auth/security-best-practices.md` (what reuse means), `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md` and `CLAUDE.md`
- [X] T019 [P] Add the row for 058 to `docs/project/timeline.md`
- [X] T020 Merge through PR #141 (squash, 2026-09-24), "fix(identity): the missing audit entries, and a stale session is not taken for theft", refs #128

## Dependencies

T005-T010 before T011-T015 (each test fails first). T011-T014 are independent of each other and of T015. T016-T019
after the handlers. T020 last.

## Notes

- 20 tasks; the four original ones are the summary, T005-T019 their breakdown.
- No Bruno request was added: no endpoint changed shape.
