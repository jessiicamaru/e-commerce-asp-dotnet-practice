# Implementation Plan: Each seller ships their own part

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Branch**: `035-seller-shipments` | **Spec**: [spec.md](spec.md) | **Closes**: #76

## Summary

Fulfil an order in **parts**: one `order_shipments` row per seller whose goods it holds, plus one for the
shop's own goods, unique on `(OrderId, SellerId)` with `NULLS NOT DISTINCT`. A seller moves their own
part (`POST /api/orders/sales/{id}/preparing`, `/shipment`); the administrator's existing endpoints now
move the shop's part. Every move locks the order row, creates any missing parts in the order's own
state, runs one guarded `UPDATE` on the part, and rewrites `orders.Status` and `TrackingReference` as a
summary - in values an older image can parse - in one transaction. The customer sees each parcel.
Decisions in [research.md](research.md).

## Technical Context

Order service only, plus the storefront. No new message, no contract in `Ecommerce.Contracts`, no
other service touched.

- **Domain**: `OrderShipment` entity, `ShipmentStatus { Pending, Preparing, Shipped }`.
- **Persistence**: `order_shipments`, unique `(OrderId, SellerId) NULLS NOT DISTINCT`; migration
  creates the table and backfills every existing order (research D3).
- **Checkout**: `SubmitOrderCommandHandler` stages one part per distinct seller with the order.
- **Moves**: one repository method, `TryMoveShipmentAsync(orderId, sellerId-or-shop, from, to, tracking)`:
  lock the order row, create missing parts, guarded update of the part, recompute the order summary -
  one transaction (research D5).
- **Seller**: `PrepareMySaleCommand`, `ShipMySaleCommand`, routes under `/api/orders/sales/{id}`.
- **Admin**: `PrepareOrderCommand`/`ShipOrderCommand` move the shop's part; the fulfilment queue reads
  the shop's part.
- **Reads**: sale status/tracking/address from the seller's part; order detail gains `shipments`;
  order summary gains `shipmentCount`, `shipmentsShipped`.
- **Client**: seller sale page actions + tracking dialog + address; customer order page parcels;
  orders list "1 of 2 shipped".

**Language/Version**: C# / .NET 10; TypeScript / React 19 in `client/`

**Primary Dependencies**: EF Core with Npgsql (`ExecuteUpdateAsync`, `ExecuteSqlInterpolatedAsync`,
`CreateExecutionStrategy`), MediatR, FluentValidation; TanStack Query and shadcn/ui in the client

**Storage**: `ecommerce_order_db` (5434), migration `20260923093713_AddOrderShipments`

**Testing**: xUnit against real PostgreSQL (`ShipmentTests`, 16 tests; `FulfilmentTests` unchanged in
intent); Vitest; Bruno; `verify-saga.sh`; a mutation run over seven guards

**Target Platform**: Order on 5059 (8080 in a container), through the gateway on 5000

**Constraints**: `orders.Status` gains no value (a rolled-back image must parse every row); the move runs
inside `CreateExecutionStrategy()` because production enables `EnableRetryOnFailure`; moves on one order
serialise, moves on different orders do not wait

**Scale/Scope**: a handful of parts per order; one person clicking at a time per part

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Order only. |
| II - Clean Architecture | **Pass.** Parts and moves behind `IOrderRepository`; the SQL lives in Infrastructure. |
| III - Atomic writes, idempotent messaging | **Pass.** Parts are written in the order's own save. Every move is a guarded single `UPDATE`; a repeated step affects zero rows and is answered as a no-op. The summary is recomputed inside the same transaction, after the order row is locked. |
| IV - Identity from the token | **Pass.** The seller's part is chosen by the token. No seller id or shipment id is accepted. Not yours is 404. |
| V - Evidence over assumption | **Pass.** The concurrency case is a test, not an argument. Each guard is removed once to see a test go red. |
| Schema compatibility | **Pass.** Additive table; `orders.Status` gains no value, so a rolled-back image still parses every row (research D4); orders it writes get their parts on demand (D3). |

No Complexity Tracking entries.

**Post-design re-check**: unchanged. One constraint surfaced while building and was met inside the
design rather than around it: production's `EnableRetryOnFailure` refuses a transaction the caller opens
itself, so the move runs through `CreateExecutionStrategy().ExecuteAsync`, which is safe to retry because
every write in it is guarded or `ON CONFLICT DO NOTHING`.

## Project Structure

### Documentation (this feature)

```text
specs/035-seller-shipments/
├── spec.md
├── plan.md                  # this file
├── research.md              # D1-D8
├── data-model.md            # order_shipments, the backfill, the summary
├── quickstart.md
├── contracts/
│   └── api.md               # seller moves, the shop's part, the new response fields
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull request)

```text
server/src/Services/Order/
├── Ecommerce.Order.Domain/
│   ├── Entities/{Order.cs,OrderShipment.cs}                       # Shipments navigation; new entity
│   └── Enums/ShipmentStatus.cs                                    # new
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/IOrderRepository.cs                      # EnsureShipmentsAsync, TryMoveShipmentAsync
│   ├── Orders/Commands/Fulfilment/{FulfilmentStep.cs,PrepareOrderCommandHandler.cs,ShipOrderCommandHandler.cs}
│   ├── Orders/Commands/SellerFulfilment/SellerFulfilmentCommands.cs   # new
│   ├── Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs   # parts at checkout
│   └── Orders/Common/{OrderMapping.cs,OrderResponses.cs,SaleResponses.cs,ShipmentMove.cs}
├── Ecommerce.Order.Infrastructure/
│   ├── Migrations/20260923093713_AddOrderShipments.cs
│   ├── Persistence/Configurations/OrderShipmentConfiguration.cs   # new
│   ├── Persistence/OrderDbContext.cs
│   └── Persistence/Repositories/OrderRepository.cs
└── Ecommerce.Order.WebApi/Controllers/OrdersController.cs         # two seller routes
server/tests/Ecommerce.Order.Tests/{ShipmentTests.cs,SellerSalesTests.cs}
client/src/components/order/order-shipments/{index.tsx,index.test.tsx}   # new
client/src/components/seller/sale-actions/index.tsx                      # new
client/src/pages/{order,orders,shop-sale}/index.tsx, shop-sale/index.test.tsx, cart/index.test.tsx
client/src/services/order/{index.ts,types.ts}, client/src/hooks/order/index.ts
client/src/locales/{en,vi}/{orders,seller}.json
client/vitest.config.ts                                                  # testTimeout 15 s
bruno/seller/a seller cannot ship a part that is not theirs.yml
bruno/security-checks/{a customer cannot ship a sale is 403,preparing a sale without a token is 401}.yml
CLAUDE.md, .specify/feature.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |

## Verification

- Order tests 81 → about 100, including concurrent shipping and every refusal.
- The existing `FulfilmentTests` pass **unchanged in intent**: an order of shop goods behaves as before.
- `verify-saga.sh`, Bruno, client tests.
- End to end: Minh's two-seller order - Mai ships hers, the order reads 1 of 2; Tuấn ships his, it
  reads shipped.

**What the pull request recorded** (#79): Order 81 → **98**; all backend 349 pass; client 98 → **104**,
lint clean, three full runs green; Bruno **103/103** requests, **164/164** tests; `verify-saga.sh`
unchanged and passing; end to end on demo data **19/19**. The backfill left **0** orders without parts on
the local stack. Seven guards removed one at a time, each turning a test red (the table is in
[tasks.md](tasks.md)). The end-to-end run as recorded differs from the plan's sketch: on Minh's order
Mai shipped and the order read "1 of 2"; the "order becomes Shipped" step was shown on An's order
(shop Ricoh + Tuấn's card), where Tuấn shipped his part, the order stayed `Preparing`, and staff then
prepared and shipped the shop's part.

## What this feature does not finish

- The delivery charge is not split between parts (D8) - specs/037 did that.
- A parcel does not say which shop it comes from - specs/036 did that.
- A part cannot be cancelled or returned; cancelling is whole-order (specs/039), returns are specs/066.
- Nothing records delivery; "shipped" is the last state here - specs/040 added delivered.
