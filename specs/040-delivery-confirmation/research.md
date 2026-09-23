# Research: Confirming a parcel arrived

## D1 - Columns, not a status

**Decision**: `order_shipments` gains `ShippedAt`, `DeliveredAt` and `DeliveryConfirmedBy`
(`"Customer"` | `"Auto"`). The part's `Status` stays `Shipped`; delivered is `Status = Shipped AND
DeliveredAt IS NOT NULL`. `orders.Status` is untouched too.

A `Delivered` value in `ShipmentStatus` (or `OrderStatus`) would stop an image rolled back to before
this from reading any delivered row - the rule every feature since specs/035 has followed. Responses
carry `deliveredAt`; "delivered" is a view, not a stored state.

## D2 - The shipped time is recorded, not inferred

`ShippedAt` is written by the move to `Shipped`. `UpdatedAt` would do today, but it is "last changed",
and the first later write to the part would silently restart the 7 days. The migration backfills
`ShippedAt = UpdatedAt` for parts already shipped - true for them, because nothing moves a part after
it ships.

## D3 - One guarded statement per confirmation, and per sweep

Customer: `UPDATE … SET DeliveredAt, DeliveryConfirmedBy WHERE Id = @part AND OrderId = @order AND
Status = 'Shipped' AND DeliveredAt IS NULL` with the owner checked in the same query. Sweep: the same
guard plus `ShippedAt <= now - period`, in one statement, so two instances sweeping at once, or a
customer confirming during a sweep, set it once - whoever is first is recorded.

## D4 - The sweeper lives in Order, like Inventory's

A `BackgroundService` in Order's Infrastructure, on a `PeriodicTimer`
(`Delivery:SweepIntervalMinutes`, default 60), sending `AutoConfirmDeliveriesCommand(cutoff)`. It is
the same shape as Inventory's `ReservationExpirySweeper`; a failed sweep is logged and the next tick
tries again. `Delivery:AutoConfirmDays` (7) is required and must be at least 1.

## D5 - Parcels need an id on the wire

`ShipmentResponse` had none - nothing needed to name one parcel before. It gains `Id` (the part's id),
so the customer's request names the parcel it confirms.
