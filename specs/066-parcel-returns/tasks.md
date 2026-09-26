---
description: "Task list for Returning a delivered parcel (part 1 - the server)"
---

# Tasks: Returning a delivered parcel (part 1 - the server)

> Completed on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/](contracts/)

**Tests**: Included, and written first - the guarantees (one row per parcel, once-only refund and restock, the claim
agreeing with the balance) belong to the database and are tested against real PostgreSQL.

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 [US1] [US2] [US3] Tests first in `Ecommerce.Order.Tests/ReturnTests.cs`. Cover:
  - request rules: owner only, delivered only, inside the window, once, and once when two requests race;
  - accept and refuse by the right party only;
  - escalate, and an admin's final word;
  - sent back within the window;
  - received publishes the event with the right amount;
  - every late or second move is 409;
  - the notices.
- [X] T002 [US4] Tests (in `Ecommerce.Order.Tests/ReturnTests.cs`, beside the flow). Cover:
  - due only after the window;
  - an open return holds the money;
  - a returned part is never money;
  - the payout claim agrees with the balance.
- [X] T003 Order: the entity, configuration, migration, repository, commands, routes, read models and notices.
- [X] T004 Payment: `refunds.ReturnId` and its indexes, the consumer and the command, with tests.
- [X] T005 Inventory: `returned_parcels`, the consumer and the command, the announcement, with tests (including `AnnouncementTests`).
- [X] T006 Storefront words for the five notices. Bruno: the round trip on the shop's parcel, and the negative cases.
- [X] T007 End to end, mutation checks, docs.

## The same work, broken down by file (added in the backfill; every item was done in #149)

### Phase 1: Contract and shared declarations

- [X] T008 [P] Add `ParcelReturnedEvent` and `ReturnedItemDto` in `server/src/BuildingBlocks/Ecommerce.Contracts/Order/ParcelReturnedEvent.cs`
- [X] T009 [P] Declare `ReturnRequested`, `ReturnAccepted`, `ReturnRefused`, `ReturnSentBack`, `ReturnRefunded` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` and `Notifier.cs`

### Phase 2: Order - foundational

- [X] T010 [P] `ReturnStatus` enum in `server/src/Services/Order/Ecommerce.Order.Domain/Enums/ReturnStatus.cs`
- [X] T011 [P] `ParcelReturn` entity in `server/src/Services/Order/Ecommerce.Order.Domain/Entities/ParcelReturn.cs`, and `OrderShipment.Return`
- [X] T012 `ParcelReturnConfiguration` (unique `ShipmentId`, status as text, indexes) in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/ParcelReturnConfiguration.cs`
- [X] T013 Migration `20260925084738_AddParcelReturns` in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/`
- [X] T014 `ReturnRepository` with `TryRequestAsync` (`ON CONFLICT ("ShipmentId") DO NOTHING`) and `TryMoveAsync` (one guarded `ExecuteUpdate` plus `stage`, inside an execution strategy) in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/ReturnRepository.cs`
- [X] T015 `ReturnOptions` (`Returns:WindowDays` = 7) in `ReturnFeatures.cs` and `server/src/Services/Order/Ecommerce.Order.WebApi/appsettings.json`

### Phase 3: US1-US3 - the flow

- [X] T016 [US1] [US2] [US3] Commands, validators and `ReturnHandlers` in `server/src/Services/Order/Ecommerce.Order.Application/Returns/ReturnFeatures.cs`
- [X] T017 [US1] [US2] [US3] `ReturnsController` - buyer, seller and Admin routes and `GET returns` - in `server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/ReturnsController.cs`
- [X] T018 [US1] `ShipmentResponse.Return`, `SaleDetailResponse.Return`, and the `ThenInclude(s => s.Return)` reads in `OrderResponses.cs`, `SaleResponses.cs`, `OrderMapping.cs`, `OrderRepository.cs`
- [X] T019 [US3] Payment: `Refund.ReturnId`, `RefundConfiguration` partial indexes, migration `20260925085607_AddReturnRefunds`, `RefundReturnCommand` in `server/src/Services/Payment/Ecommerce.Payment.Application/Payments/RefundReturn/`, `RefundReturnedParcelConsumer`; `GetRefundsAsync` reads whole-order refunds only
- [X] T020 [P] [US3] Payment tests `server/tests/Ecommerce.Payment.Tests/ReturnRefundTests.cs` (5); `RefundTests` and `ConcurrentInsertRecoveryTests` adjusted
- [X] T021 [US3] Inventory: `ReturnedParcel`, `ReturnedParcelConfiguration`, migration `20260925090112_AddReturnedParcels`, `ReturnedParcels` claim, `RestockReturnedParcelCommand`, `RestockReturnedParcelConsumer`
- [X] T022 [P] [US3] Inventory tests `server/tests/Ecommerce.Inventory.Tests/RestockReturnTests.cs` (4) and one more case in `AnnouncementTests.cs`

### Phase 4: US4 - the money hold

- [X] T023 [US4] `PayoutRepository.Money` (object initializer, so EF can group over it), `Earning` excluding received returns, and the claim's `NOT EXISTS` in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/PayoutRepository.cs`
- [X] T024 [US4] Move the five tests that assumed "delivered now means due now" past the window in `DeliveryTests.cs` and `PayoutTests.cs`; `OrderTestFixture` registers `IReturnRepository` and `ReturnOptions` and gains `ReturnWindow.PassAsync`, which moves an order's deliveries to 8 days ago

### Phase 5: Polish

- [X] T025 [P] Words for the five notices in `client/src/locales/{en,vi}/notifications.json`
- [X] T026 [P] Bruno: eight requests in `bruno/admin-audit/` (the round trip, stock checked coming back) and two in `bruno/security-checks/`
- [X] T027 Mutation checks - nine mutations, each reverted and the file touched so MSBuild rebuilt (table in the PR)
- [X] T028 [P] Docs: new `docs/features/returns.md`; the "due" rule in `fulfilment-and-delivery.md` and `marketplace.md`; reference regenerated (124 endpoints, 24 messages, 39 tables); timeline, backlog, decision 50, CLAUDE.md
- [X] T029 Merged as #149 on 2026-09-25 (`39c5ce6`), Refs #107; the issue stayed open for part 2

## Dependencies

T008-T015 before the flow; T016-T018 before T019-T022 (they consume the event the flow publishes); T023-T024 after the
entity exists; polish last. Tests (T001, T002) were written before the code they describe.

## Implementation notes

- Renaming `ReturnedItem` to `ReturnedItemDto` (the contracts' convention for a line in a message) happened late; Order
  209/209 and Inventory 51/51 were run again after it.
- EF could not `GroupBy` over a projection built with a positional record's constructor; `CancellationTests` caught it,
  and `Money` uses an object initializer.
- Deviation from the usual folder-per-use-case: the seven return commands share one file, `ReturnFeatures.cs`.
