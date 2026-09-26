# Implementation Plan: Behaviour promised but not held by a test

**Branch**: `094-untested-promises` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #186

## Summary

Fourteen tests for the seven rows of #186, each proven by a mutation that reverts its behaviour, and one validator:
`GetReviewsForStaffQueryValidator` holds the staff review list to the paging rule every other Catalog list has. The
Order test fixture gains `RestartedWith(...)` so a test can read an order through the same service restarted with a
different tax rate.

## Technical Context

| Row | Test | Where |
| :-- | :-- | :-- |
| 1 | reset / restore toasts after a remount (x4) | `client/src/pages/admin-emails/index.test.tsx`, `client/src/pages/admin-wording/index.test.tsx` |
| 2 | bundled words when the wording fetch fails, and the control | `client/src/hooks/notification-wording/index.test.tsx` (new) |
| 3 | `A_rate_changed_after_the_order_does_not_change_it` | `server/tests/Ecommerce.Order.Tests/TotalsPersistenceTests.cs`, `OrderTestFixture.cs` |
| 4 | `The_saga_relays_the_currency_to_Payment_and_the_variant_to_Inventory` | `server/tests/Ecommerce.Orchestrator.Tests/OrderStateMachineTests.cs` |
| 5 | `Simultaneous_retries_of_one_email_send_it_once` | `server/tests/Ecommerce.Identity.Tests/EmailDeliveryTests.cs` |
| 6 | the `ShopName` assertion; `Two_simultaneous_applications_leave_one_waiting` | `server/tests/Ecommerce.Identity.Tests/ShopApplicationTests.cs` |
| 7 | `The_staff_list_is_paged_like_every_other` (4 cases) + the validator | `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs`, `.../Catalog.Application/Reviews/ReviewFeatures.cs` |

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: xUnit, MassTransit test harness, FluentValidation; Vitest, Testing Library, TanStack Query

**Storage**: none new; the server tests run against real PostgreSQL

**Testing**: the feature is tests; each is mutation-checked (quickstart Scenario 4)

**Target Platform**: Identity, Order, Orchestrator, Catalog test suites; the storefront's unit tests; one Bruno request

**Performance Goals**: n/a

**Constraints**: production code changes only where row 7 needs the validator

**Scale/Scope**: 14 tests, 1 validator, 1 fixture hook, 1 Bruno request

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each test sits in the project of the service that owns the behaviour; nothing tests one service through another. |
| **II. Clean Architecture Layering** | **Pass.** The one production change is a validator in Catalog's Application layer, beside its query, picked up by the existing `ValidationBehavior`. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Rows 5 and 6 are this principle's guarded statement and unique index, now under concurrent load; row 4 is the relayed contract. |
| **IV. Identity Comes From the Token** | **Pass.** No request handling changes; row 7's route keeps `[Authorize(Roles = StaffRoles.Staff)]`. |
| **V. Evidence Over Assumption** | **Pass - the purpose.** Each row's behaviour is now evidenced by a test, and each test by a mutation that turns it red; row 2's first two mutations were not caught, and research D3 records why rather than choosing a mutation that flatters the test. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/094-untested-promises/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D6
├── data-model.md        # The database guarantees the concurrency tests exercise
├── quickstart.md        # Including every mutation
├── contracts/
│   └── validation.md    # GET /api/reviews paging, before and after
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context, plus `bruno/reviews/a page of the staff list holds at most 50.yml` (`seq: 6`),
`docs/project/backlog.md`, `docs/project/timeline.md`, `docs/features/ratings-and-reviews.md`.

No endpoint, message, table or gateway route is added or renamed, so `docs/reference/` is not regenerated.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Only #186's rows. Other untested behaviour, if any, is not surveyed here.
