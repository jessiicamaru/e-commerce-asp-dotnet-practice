---
description: "Task list for Behaviour promised but not held by a test"
---

# Tasks: Behaviour promised but not held by a test

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [quickstart.md](quickstart.md)

**Tests**: the feature is tests. Row 7's failed before its fix; every other row's is shown by a mutation.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server rows

- [X] T001 [P] [US1] [US2] Row 7: `The_staff_list_is_paged_like_every_other` in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs` - red; then `GetReviewsForStaffQueryValidator` in `.../Catalog.Application/Reviews/ReviewFeatures.cs`
- [X] T002 [P] [US1] Row 6: the `ShopName` assertion and `Two_simultaneous_applications_leave_one_waiting` in `server/tests/Ecommerce.Identity.Tests/ShopApplicationTests.cs`
- [X] T003 [P] [US1] Row 5: `Simultaneous_retries_of_one_email_send_it_once` in `server/tests/Ecommerce.Identity.Tests/EmailDeliveryTests.cs`
- [X] T004 [P] [US1] Row 3: `OrderTestFixture.RestartedWith` and `A_rate_changed_after_the_order_does_not_change_it` in `server/tests/Ecommerce.Order.Tests/`
- [X] T005 [P] [US1] Row 4: `The_saga_relays_the_currency_to_Payment_and_the_variant_to_Inventory` in `server/tests/Ecommerce.Orchestrator.Tests/OrderStateMachineTests.cs`

## Phase 2: Storefront rows

- [X] T006 [P] [US1] Row 1: reset and restore after a remount - `client/src/pages/admin-emails/index.test.tsx` (2), `client/src/pages/admin-wording/index.test.tsx` (2)
- [X] T007 [P] [US1] Row 2: `client/src/hooks/notification-wording/index.test.tsx` (new) - failure keeps the bundle; control applies an edit

## Phase 3: Verification and docs

- [X] T008 Every mutation in quickstart Scenario 4 - each red, each reverted
- [X] T009 Suites: Identity, Order, Orchestrator, Catalog; storefront 472/472, lint and types clean
- [X] T010 [P] Bruno `bruno/reviews/a page of the staff list holds at most 50.yml` (`seq: 6`)
- [X] T011 Docs: `docs/features/ratings-and-reviews.md`, `docs/project/backlog.md`, `docs/project/timeline.md`
- [ ] T012 Merged as #201, closing #186

## Verification

- Row 7 before its fix: 4 of 4 cases red.
- Mutations: see quickstart Scenario 4 - all caught; row 2 caught only when both of its guards were broken (research D3).
