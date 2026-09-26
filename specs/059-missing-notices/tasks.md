---
description: "Task list for The notices nobody got"
---

# Tasks: The notices nobody got

> Completed on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first, on both sides: each notice has a server test, and the storefront's
contract test failed first on the four kinds it had no words for (constitution Principle V).

## Format: `[ID] [P?] [Story] Description`

The first five tasks are the list as written on 2026-09-24, kept verbatim. T006 onwards break them down.

- [X] T001 [US3] Declare the four kinds: `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`, `notification-kinds.json`
- [X] T002 [US1] [US2] Server tests first: `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs` (lock, ban), `server/tests/Ecommerce.Order.Tests/NotificationTests.cs` (the sweep), `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs` (hide)
- [X] T003 [US1] [US2] Notices in `UserAdministration.cs`, `DeliveryCommands.cs` + `OrderNotices.cs`, `ReviewFeatures.cs`
- [X] T004 [US3] Storefront words and `until` formatting: `client/src/utils/notifications/index.ts`, `client/src/locales/{en,vi}/notifications.json`, the two callers; the contract test fails first
- [X] T005 Mutation checks; docs `docs/features/audit-and-notifications.md`, `docs/project/*`

---

## Phase 1: Foundational - the declaration

- [X] T006 [US3] Add `ParcelAutoDelivered`, `AccountLocked`, `AccountBanned` and `ReviewHidden` constants to `NotificationKind` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`
- [X] T007 [US3] Declare their required keys in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`: `orderId`; `until`, `reason`; `reason`; `product`, `reason`

## Phase 2: Tests first

- [X] T008 [P] [US1] Extend `Every_action_is_on_the_record_with_its_diff_and_a_grant_tells_the_person` in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`: a 2-day lock sends one `AccountLocked` with the reason and an `until` more than a day ahead; a ban sends one `AccountBanned` with its reason
- [X] T009 [P] [US2] Add `The_sweep_taking_a_parcel_as_delivered_tells_its_seller` to `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`: ship a seller's parcel, age `ShippedAt`, run `AutoConfirmDeliveriesCommand`; one `ParcelAutoDelivered` to the seller linked to the sale, no `ParcelReceived`
- [X] T010 [P] [US1] Extend `A_hidden_review_is_neither_shown_nor_counted` in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs`: one `ReviewHidden` to the author with product, reason and link, and `NotificationContract.Problems` empty
- [X] T011 [P] [US3] In `client/src/utils/notifications/index.test.ts` add "words a lock with its end in the reader language and the reason", an `until` sample for the per-kind contract test, and pass the language to `describeNotification`

## Phase 3: User Story 1 - people hear about what staff did to them (P1)

- [X] T012 [US1] In `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`, notify `AccountLocked` (`until` as UTC `"o"`, `reason`) and `AccountBanned` (`reason`) after the audit entry and before the one save
- [X] T013 [US1] In `server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs`, read the product, and in the `TryHideAsync` stage record the audit entry and notify the author with `product`, `reason` and `/products/{productId}`

## Phase 4: User Story 2 - a seller hears when the sweep delivers their parcel (P1)

- [X] T014 [US2] Add `OrderNotices.AutoDeliveredAsync` to `server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/OrderNotices.cs`: the parcel's seller, `ParcelAutoDelivered`, `/shop/sales/{orderId}`; nothing for a null seller
- [X] T015 [US2] Inject `INotifier` into `AutoConfirmDeliveriesCommandHandler` in `server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/ConfirmDelivery/DeliveryCommands.cs` and, inside the sweep's stage, call it for each parcel from `GetDeliveredParcelsAsync` through `OrderNotices.WithFactsAsync`

## Phase 5: User Story 3 - the storefront has words for all of them (P1)

- [X] T016 [US3] Add the third `language` parameter to `describeNotification` in `client/src/utils/notifications/index.ts`, formatting `until` with `toLocaleString(language)`
- [X] T017 [P] [US3] Pass `i18n.language` from `client/src/components/layout/notification-bell/index.tsx` and `client/src/pages/notifications/index.tsx`
- [X] T018 [P] [US3] Add the four sentences to `client/src/locales/en/notifications.json` and `client/src/locales/vi/notifications.json`

## Phase 6: Polish

- [X] T019 Run the three mutations - the sweep tells nobody; a hidden review tells nobody; a lock's end shown as raw UTC - and confirm each turns a test red, then restore
- [X] T020 Run Order 184/184, Identity 83/83, Catalog 161/161 against real PostgreSQL, and the storefront 291/291 with lint and `tsc` clean
- [X] T021 [P] Update `docs/features/audit-and-notifications.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`, `CLAUDE.md`; move #128 to Fixed in `docs/project/backlog.md`; add the 059 row to `docs/project/timeline.md`
- [X] T022 Merge through PR #142 (squash, 2026-09-24), "fix(order): the notices nobody got - a sweep-delivered parcel, a lock, a ban, a hidden review", closing #128

## Dependencies

T006-T007 before everything (the tests check against the declaration). T008-T011 before T012-T018. The three
server stories are independent of each other; T016-T018 depend only on T007 and T011. T019-T021 after, T022 last.

## Notes

- 22 tasks; the five original ones are the summary, T006-T021 their breakdown.
- No Bruno request was added: no endpoint changed.
