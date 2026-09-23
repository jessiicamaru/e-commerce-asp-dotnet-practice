# Implementation Plan: Each seller ships their own part

**Branch**: `035-seller-shipments` | **Spec**: [spec.md](spec.md) | **Closes**: #76

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

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Order only. |
| II - Clean Architecture | Parts and moves behind `IOrderRepository`; the SQL lives in Infrastructure. |
| III - Atomic writes, idempotent messaging | Parts are written in the order's own save. Every move is a guarded single `UPDATE`; a repeated step affects zero rows and is answered as a no-op. The summary is recomputed inside the same transaction, after the order row is locked. |
| IV - Identity from the token | The seller's part is chosen by the token. No seller id or shipment id is accepted. Not yours is 404. |
| V - Evidence over assumption | The concurrency case is a test, not an argument. Each guard is removed once to see a test go red. |
| Schema compatibility | Additive table; `orders.Status` gains no value, so a rolled-back image still parses every row (research D4); orders it writes get their parts on demand (D3). |

No Complexity Tracking entries.

## Verification

- Order tests 81 → about 100, including concurrent shipping and every refusal.
- The existing `FulfilmentTests` pass **unchanged in intent**: an order of shop goods behaves as before.
- `verify-saga.sh`, Bruno, client tests.
- End to end: Minh's two-seller order - Mai ships hers, the order reads 1 of 2; Tuấn ships his, it
  reads shipped.
