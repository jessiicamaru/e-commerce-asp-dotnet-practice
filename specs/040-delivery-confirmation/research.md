# Research: Confirming a parcel arrived

> Completed on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Decisions as written before the code; labels added so each
> reads like specs/001. Who confirms, and after how long, were decided with the user (spec.md).

**Feature**: [spec.md](spec.md)

## D1 - Columns, not a status

**Decision**: `order_shipments` gains `ShippedAt`, `DeliveredAt` and `DeliveryConfirmedBy`
(`"Customer"` | `"Auto"`). The part's `Status` stays `Shipped`; delivered is `Status = Shipped AND
DeliveredAt IS NOT NULL`. `orders.Status` is untouched too.

**Rationale**: A `Delivered` value in `ShipmentStatus` (or `OrderStatus`) would stop an image rolled back to before
this from reading any delivered row - the rule every feature since specs/035 has followed. Responses
carry `deliveredAt`; "delivered" is a view, not a stored state.

**Alternatives considered**: a `Delivered` value in `ShipmentStatus` or `OrderStatus` - rejected above.

## D2 - The shipped time is recorded, not inferred

**Decision**: a `ShippedAt` column, written by the ship move.

**Rationale**: `ShippedAt` is written by the move to `Shipped`. `UpdatedAt` would do today, but it is "last changed",
and the first later write to the part would silently restart the 7 days. The migration backfills
`ShippedAt = UpdatedAt` for parts already shipped - true for them, because nothing moves a part after
it ships.

As built, parts created on demand for an older image's order in `Shipped` get `ShippedAt` from the
order's `UpdatedAt` in the same statement (`EnsureShipmentsAsync`), and a move to any other state writes
it null.

**Alternatives considered**: counting from `UpdatedAt` - rejected above, the first later write would
restart the week. Removing the shipped-time write was one of the pull request's mutations, and a test
caught it.

## D3 - One guarded statement per confirmation, and per sweep

**Decision**: both paths are a single guarded `UPDATE`.

**Rationale**: Customer: `UPDATE … SET DeliveredAt, DeliveryConfirmedBy WHERE Id = @part AND OrderId = @order AND
Status = 'Shipped' AND DeliveredAt IS NULL` with the owner checked in the same query. Sweep: the same
guard plus `ShippedAt <= now - period`, in one statement, so two instances sweeping at once, or a
customer confirming during a sweep, set it once - whoever is first is recorded.

As built, when the customer's statement affects no row the parcel is read again, still through the owner
filter, to say why: not there (404), already delivered (200, no-op) or not shipped (409).

**Alternatives considered**: none recorded. "The sweep overwriting a customer's confirmation" was one of
the pull request's mutations, and a test caught it.

## D4 - The sweeper lives in Order, like Inventory's

**Decision**: a hosted service in Order's Infrastructure.

**Rationale**: A `BackgroundService` in Order's Infrastructure, on a `PeriodicTimer`
(`Delivery:SweepIntervalMinutes`, default 60), sending `AutoConfirmDeliveriesCommand(cutoff)`. It is
the same shape as Inventory's `ReservationExpirySweeper`; a failed sweep is logged and the next tick
tries again. `Delivery:AutoConfirmDays` (7) is required and must be at least 1.

As built, `DeliveryOptions.Register` binds the `Delivery` section and validates on start that both
`AutoConfirmDays` and `SweepIntervalMinutes` are at least 1 - zero days would pay a seller the moment they
click "shipped". The cutoff comes from an injected `TimeProvider`.

**Alternatives considered**: none recorded for this feature; the shape follows Inventory's sweeper,
whose own record (specs/001 D4) rejected message scheduling.

## D5 - Parcels need an id on the wire

**Decision**: `ShipmentResponse` gains `Id`.

**Rationale**: `ShipmentResponse` had none - nothing needed to name one parcel before. It gains `Id` (the part's id),
so the customer's request names the parcel it confirms.

**Alternatives considered**: none recorded.
