# Implementation Plan: Returning a delivered parcel (part 1 - the server)

> Completed on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Branch**: `066-parcel-returns` | **Spec**: [spec.md](spec.md) | **Issue**: #107 | **PR**: #149 (merged 2026-09-25)

## Summary

A buyer asks to return a whole delivered parcel within 7 days; the parcel's seller (an administrator for the shop's
own) accepts or refuses; a refusal can be escalated to an administrator for the final word; the buyer sends it back
with a tracking reference; the seller marks it received. Receiving it publishes `ParcelReturnedEvent`, from which
Payment records one refund and Inventory puts the units back once. A seller's money is due only once the return
window has passed with no return open - a hold, never a debt. Reasoning in [research.md](research.md).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (EF outbox), MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core,
`Ecommerce.Shared` (`IAuditTrail`, `INotifier`, `ICurrentUser`), `Ecommerce.Contracts`

**Storage**: PostgreSQL - Order (`parcel_returns`), Payment (`refunds.ReturnId`), Inventory (`returned_parcels`); one
migration in each

**Testing**: xUnit against real PostgreSQL - Order on 5434, Payment on 5438, Inventory on 5437; Bruno through the
gateway

**Target Platform**: the Order, Payment and Inventory services behind the gateway on :5000

**Project Type**: backend, three existing Clean Architecture services

**Constraints**: every step one guarded statement plus its outbox writes in one transaction; one refund and one
restock per return whatever the delivery count; the balance and the payout claim must agree on "due"

**Scale/Scope**: one row per returned parcel; no new service, no new gateway route (`/api/orders/**` already routes)

## Design

**Order**
- **`parcel_returns`** columns:
  - `Id`, `OrderId`, `ShipmentId` (**unique**), `CustomerId`, `SellerId` (null means the shop);
  - `Status` as text: Requested, Accepted, Refused, Escalated, Rejected, SentBack, Received;
  - `Reason`, `DecisionReason`, `TrackingReference`;
  - `RequestedAt`, `DecidedAt`, `SentBackAt`, `ReceivedAt`, `RefundAmount`, `UpdatedAt`.
- **Request:** the parcel must be the caller's, delivered, and inside the window. The row is inserted
  with `ON CONFLICT ("ShipmentId") DO NOTHING`, so two requests at once make one row.
- **Every move** is a guarded `UPDATE parcel_returns ... WHERE "Id" = @id AND "Status" = @from [AND the
  window]`, run in a transaction together with the `stage` callback (audit, notices, event).
- **Received** computes the refund from the parcel's lines (the `order_items` of `(OrderId, SellerId)`:
  `UnitPrice × Quantity + TaxAmount`). It stages `ParcelReturnedEvent(ReturnId, OrderId, ShipmentId,
  Items[VariantId ?? ProductId, Quantity], Amount, Currency, ReturnedAt)`.
- **The money hold:** a part is due when it was delivered more than the window ago and no return of it is
  open, and a returned part is excluded from earnings. The same rule appears in LINQ (balance, due list)
  and in the claim's SQL, and a test checks that the two agree.
- **Routes:**
  - Customer: `return`, `return/escalate`, `return/sent`.
  - Seller: `sales/{id}/return/accept|refuse|received`.
  - Administrator: `fulfilment/{id}/shipments/{shipmentId}/return/accept|refuse|received`, for the shop's
    parcels and the final word on escalated ones. Plus `GET returns?status=`.
- **Read models:** `ShipmentResponse.Return` and `SaleDetailResponse.Return`.
- **Notices:** `ReturnRequested`, `ReturnAccepted`, `ReturnRefused`, `ReturnSentBack`, `ReturnRefunded`.
  Each is declared in `notification-kinds.json` and worded in the storefront.

**Payment**
- `refunds.ReturnId`, nullable. The unique index on `OrderId` becomes two unique indexes:
  - on `OrderId` where `ReturnId` is null, so one cancel refund per order;
  - on `ReturnId`, so one refund per return.
- `RefundReturnedParcelConsumer` sends `RefundReturnCommand`. The command refuses (and logs) a refund
  beyond what was paid.

**Inventory**
- `returned_parcels(ReturnId PK)` is claimed with `ON CONFLICT DO NOTHING`, so a redelivery restocks
  nothing.
- `RestockReturnedParcelConsumer`:
  1. adds the quantity back to `QuantityOnHand` under `FOR UPDATE`;
  2. calls `StockAvailabilityAnnouncer`, making it the eighth handler that moves stock;
  3. records an audit entry.

## Decisions

1. **Whole parcels**, like whole-order cancellation.
2. **A hold, not a debt.** Money is due after the window, and a return can start only inside it.
3. **Goods plus tax are refunded; delivery is not.** The customer pays the return trip, which nothing in
   the system models.
4. **Returns are handled by Admin, not Moderator**, because the fulfilment endpoints are Admin
   (specs/039).

Recorded as decision 50 in [docs/project/decisions.md](../../docs/project/decisions.md); the full reasoning with the
rejected alternatives is in [research.md](research.md).

## Constitution check

- **I. Service autonomy: pass.** Each service decides from its own data. Payment refunds an amount Order
  computed from its frozen prices, and Inventory restocks what the event names.
- **III. Atomic writes, idempotent messaging: pass.** Every move is a guarded statement plus the outbox,
  in one transaction. Both consumers are idempotent on `ReturnId`.
- **IV. Identity from the token: pass.** The buyer and the seller come from the token.
- **V. Evidence over assumption: pass.** Tests across the three services, mutation checks, and an
  end-to-end return through the gateway.

The same check against all five principles of [constitution.md](../../.specify/memory/constitution.md), as a table:

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order owns returns and the prices it froze; Payment owns refunds and checks the amount against its own payment row (never more than paid, never another currency); Inventory owns stock and decides nothing about the return. The only coupling is `ParcelReturnedEvent` in `Ecommerce.Contracts` |
| **II. Clean Architecture Layering** | **Pass.** Commands, validators and `IReturnRepository` live in `Order.Application/Returns`; the guarded SQL in `Infrastructure/Persistence/Repositories/ReturnRepository.cs`; `ReturnsController` only sends through MediatR; the two consumers sit in WebApi and dispatch commands declared in Application. One deviation from the usual folder-per-use-case shape: all seven return commands and their handler are in one file, `ReturnFeatures.cs`, because they share the move and the window |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each step is one `ExecuteUpdate` guarded on the expected state, with its audit entry, notice and (for "received") the event staged through the outbox and saved inside the same transaction. The request is idempotent by the unique `ShipmentId` index, the refund by the unique `refunds.ReturnId`, the restock by the `returned_parcels` primary key - all database constraints, as the principle requires |
| **IV. Identity Comes From the Token** | **Pass.** The buyer is `ICurrentUser.Id` and the parcel query filters on `Order.UserId`; the seller's steps take no seller id, only the order id, and read their part by the token's id. No command carries a user id |
| **V. Evidence Over Assumption** | **Pass.** 24 Order, 5 Payment and 4 + 1 Inventory tests against real PostgreSQL (the guarantees are the database's unique indexes and guarded updates); nine mutations each caught by a named test; Bruno's round trip through the gateway with the stock checked coming back over the broker; the resulting rows read from all three databases |

**Post-design re-check**: no violations. The one thing that needed care - "due" written twice, in LINQ and SQL - is not
a violation but a risk, answered by a test that holds the two together (research D4).

## Project Structure

### Documentation (this feature)

```text
specs/066-parcel-returns/
├── spec.md
├── plan.md              # this file
├── research.md          # D1-D7
├── data-model.md        # parcel_returns, refunds.ReturnId, returned_parcels, the state machine
├── contracts/
│   ├── http-api.md      # the nine return endpoints and the two read-model fields
│   └── messages.md      # ParcelReturnedEvent and the five notices
├── quickstart.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (touched at the merge)

```text
server/src/BuildingBlocks/
├── Ecommerce.Contracts/Order/ParcelReturnedEvent.cs                 # new
└── Ecommerce.Shared/Notifications/{Notifier.cs, notification-kinds.json}
server/src/Services/Order/
├── Ecommerce.Order.Domain/Entities/{ParcelReturn.cs, OrderShipment.cs}, Enums/ReturnStatus.cs
├── Ecommerce.Order.Application/Returns/ReturnFeatures.cs            # commands, validators, handler, options
├── Ecommerce.Order.Application/Orders/Common/{OrderResponses.cs, SaleResponses.cs, OrderMapping.cs}
├── Ecommerce.Order.Infrastructure/Persistence/Configurations/ParcelReturnConfiguration.cs
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/{ReturnRepository.cs, PayoutRepository.cs, OrderRepository.cs}
├── Ecommerce.Order.Infrastructure/Migrations/20260925084738_AddParcelReturns.cs
└── Ecommerce.Order.WebApi/{Controllers/ReturnsController.cs, appsettings.json}
server/src/Services/Payment/
├── Ecommerce.Payment.Application/Payments/RefundReturn/RefundReturnCommand.cs
├── Ecommerce.Payment.Domain/Entities/Refund.cs
├── Ecommerce.Payment.Infrastructure/{Persistence/..., Migrations/20260925085607_AddReturnRefunds.cs}
└── Ecommerce.Payment.WebApi/Consumers/RefundReturnedParcelConsumer.cs
server/src/Services/Inventory/
├── Ecommerce.Inventory.Application/Reservations/RestockReturnedParcel/RestockReturnedParcelCommand.cs
├── Ecommerce.Inventory.Domain/Entities/ReturnedParcel.cs
├── Ecommerce.Inventory.Infrastructure/{Persistence/..., Migrations/20260925090112_AddReturnedParcels.cs}
└── Ecommerce.Inventory.WebApi/Consumers/RestockReturnedParcelConsumer.cs
server/tests/
├── Ecommerce.Order.Tests/ReturnTests.cs (+ DeliveryTests, PayoutTests, OrderTestFixture)
├── Ecommerce.Payment.Tests/ReturnRefundTests.cs
└── Ecommerce.Inventory.Tests/{RestockReturnTests.cs, AnnouncementTests.cs}
bruno/admin-audit/            # the round trip on the shop's parcel (8 requests)
bruno/security-checks/        # 401 and 403 for returns
client/src/locales/{en,vi}/notifications.json                         # the five notices' words
```

**Structure Decision**: the feature sits in the three services that own its facts; no new service and no new gateway
route.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- **No screens.** The storefront gets only the words for the five notices; the pages are part 2
  ([specs/067](../067-return-screens/)).
- **Every seller's money becomes due 7 days later than before**, because "due" now waits for the return window. This
  is intended, and five existing tests were moved past the window to say so.
- **Payment is a stub**: a refund is a ledger entry and moves no money.
- **Whole parcels only**, no photos, no return shipping.
