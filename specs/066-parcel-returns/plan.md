# Implementation Plan: Returning a delivered parcel (part 1 - the server)

**Branch**: `066-parcel-returns` | **Spec**: [spec.md](spec.md) | **Issue**: #107

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

## Constitution check

- **I. Service autonomy: pass.** Each service decides from its own data. Payment refunds an amount Order
  computed from its frozen prices, and Inventory restocks what the event names.
- **III. Atomic writes, idempotent messaging: pass.** Every move is a guarded statement plus the outbox,
  in one transaction. Both consumers are idempotent on `ReturnId`.
- **IV. Identity from the token: pass.** The buyer and the seller come from the token.
- **V. Evidence over assumption: pass.** Tests across the three services, mutation checks, and an
  end-to-end return through the gateway.
