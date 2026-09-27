# Research: A seller's list of the returns of their parcels

## D1 - Filter on `parcel_returns.SellerId`, not through the order

**Decision**: the list filters `parcel_returns."SellerId" = @caller`, using the existing `IX_parcel_returns_SellerId`
index.

**Rationale**:

- The column was written when the return was requested, from the parcel's own seller (specs/066). It is exactly
  "whose parcel".
- Joining through `order_shipments` would read the same fact the long way round.

**Alternative rejected**: listing the seller's sales and asking each for its return. That is one query per sale, and
it pages over sales, not returns.

## D2 - Reuse `ReturnResponse` and `ReturnPage`

**Decision**: the same shape as staff's list, and as the `Return` a seller already reads on their sale detail.

**Rationale**:

- The sale detail has shown a seller the full `ReturnResponse`, including `RefundAmount`, since specs/067, so the
  list reveals nothing new.
- One shape means one client type.

**Alternative rejected**: a narrower seller shape. It would hide nothing the sale page does not already show.

## D3 - The badge is a subquery in the sales page

**Decision**: `GetSalesPageAsync` selects the return state of the caller's parcel of each order:
`parcel_returns WHERE "OrderId" = o."Id" AND "SellerId" = @caller`.

**Rationale**: It is one scalar subquery per row of an already-paged query, and the table has at most one return per
parcel (the unique `ShipmentId`).

## D4 - Seller role only

**Decision**: `[Authorize(Roles = "Seller")]`.

**Rationale**: It matches the other `sales/*` routes. Staff have their own list. An administrator is not a seller of
these parcels, and the list is scoped by the caller's id, so an administrator would get an empty list anyway.
