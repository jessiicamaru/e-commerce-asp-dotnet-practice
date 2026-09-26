# Implementation Plan: Confirming a parcel arrived

> Completed on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Branch**: `040-delivery-confirmation` | **Spec**: [spec.md](spec.md) | [research](research.md) |
[contracts](contracts/api.md)

## Summary

Record that a parcel arrived, and pay sellers only for parcels that did. `order_shipments` gains
`ShippedAt`, `DeliveredAt` and `DeliveryConfirmedBy` - columns, not a status. The customer confirms a
shipped parcel with one guarded `UPDATE`; a hosted sweeper takes as delivered, as "Auto", every parcel
shipped more than `Delivery:AutoConfirmDays` ago that nobody confirmed. Balances, the due list and the
payout claim require `DeliveredAt`. Decisions in [research.md](research.md).

## Technical Context

- **Order only** (plus the client). `OrderShipment.ShippedAt/DeliveredAt/DeliveryConfirmedBy`, migration
  `AddShipmentDelivery` (backfill `ShippedAt`); `TryMoveShipmentAsync` writes `ShippedAt`.
- `IOrderRepository.TryConfirmDeliveryAsync(orderId, shipmentId, ownerId, at)` and
  `AutoConfirmDeliveriesAsync(cutoff, at)`; `ConfirmDeliveryCommand`, `AutoConfirmDeliveriesCommand`.
- `DeliveryOptions` (validated at startup) + `DeliveryConfirmationSweeper` (hosted service).
- `PayoutRepository`: due and the claim require `DeliveredAt IS NOT NULL`; on the way is everything else.
- Responses: `ShipmentResponse` + `Id`, `DeliveredAt`, `DeliveryConfirmedBy`; `SaleDetailResponse` + `DeliveredAt`.
- Client: "I've received it" per shipped parcel (and on a one-parcel order), received badges, the order
  reads delivered when every parcel is; seller sees received; earnings due only once delivered.

**Language/Version**: C# / .NET 10; TypeScript / React 19; bash for `verify-saga.sh`

**Primary Dependencies**: EF Core with Npgsql (`ExecuteUpdateAsync`), MediatR, `Microsoft.Extensions.Hosting`
(`BackgroundService`, `PeriodicTimer`), `Microsoft.Extensions.Options` (`ValidateOnStart`), `TimeProvider`

**Storage**: `ecommerce_order_db` - migration `20260923183204_AddShipmentDelivery`

**Testing**: xUnit against real PostgreSQL (`DeliveryTests`, 12 tests; `PayoutTests` helper now
delivers); Vitest; Bruno; `verify-saga.sh`; server and client mutation runs

**Target Platform**: Order on 5059; the sweeper runs inside every Order instance

**Constraints**: no new enum value; every confirmation one guarded statement, safe with several
instances; the period validated at startup; the week counted from a recorded shipped time, never
`UpdatedAt`

**Scale/Scope**: one sweep per `SweepIntervalMinutes` (60) per instance, one statement per sweep

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Order's own rows; no event - nothing else needs to know. |
| II - Clean Architecture | **Pass.** Commands in Application; the sweeper and SQL in Infrastructure. |
| III - Atomic, idempotent | **Pass.** Every confirmation is one guarded UPDATE; repeats and concurrent sweeps affect nothing. |
| IV - Identity from the token | **Pass.** Owner from the token, in the same query. |
| V - Evidence | **Pass.** Tests: confirm, refusals, repeat, owner, sweep window, sweep idempotence, payouts only delivered; verify-saga confirms. |
| Schema compatibility | **Pass.** Three nullable columns; no enum value (research D1). |

**Post-design re-check**: unchanged. Principle I held at the time - no event, nothing else needed to
know. (Later features did publish from the delivery: specs/042 notified sellers and specs/046 announced
`ParcelDeliveredEvent` so Catalog knows who may review; neither is part of this feature.)

## Project Structure

### Documentation (this feature)

```text
specs/040-delivery-confirmation/
├── spec.md
├── plan.md                  # this file
├── research.md              # D1-D5
├── data-model.md            # three columns, the backfill, the index, the transitions
├── quickstart.md
├── contracts/
│   └── api.md               # one route, additive fields, configuration
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull request)

```text
server/src/Services/Order/
├── Ecommerce.Order.Domain/Entities/OrderShipment.cs                         # three properties
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/IOrderRepository.cs                                # TryConfirmDeliveryAsync, AutoConfirmDeliveriesAsync
│   ├── Orders/Commands/ConfirmDelivery/DeliveryCommands.cs                  # new: customer + auto
│   └── Orders/Common/{Delivery.cs,OrderMapping.cs,OrderResponses.cs,SaleResponses.cs}
├── Ecommerce.Order.Infrastructure/
│   ├── Delivery/{DeliveryConfirmationSweeper.cs,DeliveryOptions.cs}         # new
│   ├── DependencyInjection.cs                                               # options + hosted service
│   ├── Migrations/20260923183204_AddShipmentDelivery.cs
│   ├── Persistence/Configurations/OrderShipmentConfiguration.cs             # length, index
│   └── Persistence/Repositories/{OrderRepository.cs,PayoutRepository.cs}
└── Ecommerce.Order.WebApi/{Controllers/OrdersController.cs,appsettings.json}
server/tests/Ecommerce.Order.Tests/{DeliveryTests.cs,PayoutTests.cs}
client/src/components/order/receive-parcel/index.tsx                         # new
client/src/components/order/order-shipments/index.tsx, components/seller/sale-earnings/
client/src/utils/order/{delivery.ts,delivery.test.ts}                        # new
client/src/pages/{order,shop-sale}/..., services/order/, hooks/order/, locales/{en,vi}/{orders,seller}.json
bruno/admin-audit/{the customer says the parcel arrived,saying it arrived again changes nothing,staff read any order}.yml
bruno/seller/saying someone else s parcel arrived is 404.yml
.github/scripts/verify-saga.sh                                               # confirms the parcel it ships
CLAUDE.md, .specify/feature.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |

## Verification

**What the pull request recorded** (#85): Order **164** tests (+12), seven of the new `DeliveryTests`
seen red against stubs first (the five green were the shipped-time write, the startup validation and a
"not found" the stub answered vacuously); eight server guards removed one at a time, each turning a test
red (the due and claim filters, owner, shipped, repeat, the sweep window, the sweep overwriting a
customer's confirmation, the shipped time); `PayoutTests`' helper now delivers as well as ships, and six
of its tests went red on the change of meaning. Client **166** (+7), lint clean, build passing, 6/6
mutations caught. Bruno **121/121** requests, **192/192** tests. `verify-saga.sh` confirms the parcel it
ships and passes, including the specs/039 cancellation. The container logs show the sweeper started (7
days, checked every hour). In a browser Lan confirmed the shop's parcel on her mixed order ("Đã nhận ngày
24/9/2026") while Tuấn's parcel still waited.

## What this feature does not finish

- Disputes, returns and carrier tracking. Returns came in specs/066, which also moved "due" later: money is
  due only once a delivered parcel is past `Returns:WindowDays` with no return open.
- The sweep did not yet lock the rows it set; specs/046 made it lock first so the ids it announces are the
  ones it set.
- No message is published on delivery here; specs/042 and 046 added notices and `ParcelDeliveredEvent`.
