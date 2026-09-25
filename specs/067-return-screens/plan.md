# Implementation Plan: Returning a delivered parcel (part 2 - the screens)

**Branch**: `067-return-screens` | **Spec**: [spec.md](spec.md) | **Issue**: #107

## Design

Client only. No endpoint, message or table changes.

- **Types** (`services/order/types.ts`):
  - `ParcelReturn` mirrors `ReturnResponse`;
  - `Shipment.return` and `Sale.return` are optional and nullable;
  - `ReturnPage`.
- **Service calls:**
  - `Order.requestReturn`, `escalateReturn` and `sendReturnBack` for the buyer;
  - `Order.acceptSaleReturn`, `refuseSaleReturn` and `receiveSaleReturn` for the seller;
  - `Admin.returns`, `acceptReturn`, `refuseReturn` and `receiveReturn` for staff.
- **Rules**: `utils/order/returns.ts` holds the pure functions that decide what to *offer*:
  - `canRequestReturn`, `canEscalate`, `canSendBack` and `returnDeadline` for the buyer;
  - `sellerReturnStep` and `staffReturnStep` for the other two, each returning `decide`, `receive` or
    nothing.

  Each copies the matching server guard. `RETURN_WINDOW_DAYS` lives in `constants/order`.
- **Components**:
  - `components/order/parcel-return` is the buyer's view of one parcel. It uses its own hook,
    `useParcelReturn(orderId, shipmentId)`, so every parcel's mutations are its own.
  - `components/order/return-decision` is the seller's and staff's view: the reason, the state, and the
    accept, refuse and received steps, with the mutations handed in (the same idea as `ParcelActions`). The
    `final` flag words a refusal as "reject for good".
- **Pages**:
  - `pages/order` draws `ParcelReturn` under the single parcel. `OrderShipments` draws it inside each card
    when given `orderId`.
  - `pages/shop-sale` adds a return card when `sale.return` exists.
  - `pages/admin-order` adds a returns card with one `StaffReturn` per parcel that has a return.
  - The new `pages/admin-returns` is the queue, with its status in `?status=`. It is routed at
    `/admin/returns` and added to the menu as `adminOnly`.

## Research

- **D1 - how the client learns the 7 days.** It uses a constant, `RETURN_WINDOW_DAYS = 7`, for *drawing
  only*.
  - The alternative was a `returnableUntil` field on `ShipmentResponse`. That would have meant a server change
    to a static mapping that has no options, in a PR that is otherwise client-only.
  - If the two ever disagree, the server refuses with a 409 that names the window, and the page shows it. The
    same bargain as `customerCanCancel`.
- **D2 - one decision component for the seller and staff.** Same steps, same confirmation. Only the
  endpoints differ, and those are handed in. The same idea as `ParcelActions` (specs/038).
- **D3 - the queue shows no amounts.** `ReturnResponse` carries no currency, and a number without its
  currency is the thing specs/022 forbids. The amount is on the order page, in the order's currency.

## Constitution check

- **IV (identity from the token):** no id of the caller is sent anywhere. The server decides whose parcel it
  is.
- **V (evidence):** there are Vitest tests for every rule and action, and the whole round trip through the
  storefront container is recorded in the PR.
