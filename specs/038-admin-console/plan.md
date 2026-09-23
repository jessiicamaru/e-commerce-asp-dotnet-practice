# Implementation Plan: An administrator's console

**Branch**: `038-admin-console` | **Spec**: [spec.md](spec.md) | [contracts](contracts/api.md)

## Technical Context

- **Order**: `GetOrderForStaffQuery` → `OrderMapping.ToDetail` over `IOrderRepository.GetByIdAsync`
  (items and parts loaded); route `GET /api/orders/fulfilment/{id}`, `[Authorize(Roles = "Admin")]`.
  No other server change - every other action already has an endpoint.
- **Client**:
  - `isAdmin` on the auth context (from `roles`, like `isSeller` - for drawing, never deciding).
  - `layouts/admin-layout`, routes `/admin` (fulfilment queue), `/admin/orders/:id`, `/admin/payouts`,
    all under `RequireRole role="Admin"`; a header link and a user-menu entry for administrators.
  - `SaleActions` becomes `components/order/parcel-actions`, taking a status, a tracking reference and
    the two mutations - one component for a seller's parcel and the shop's, because the steps and the
    rule (next step only) are the same.
  - `services/admin` (`Admin.fulfilment`, `Admin.order`, `Admin.prepare`, `Admin.ship`, `Admin.due`,
    `Admin.pay`), `hooks/admin`, namespace `admin` in `locales/`.

## Research

- **D1 - The shop's parcel is read from `shipments[]`**, the part with `isShop`. An order an older image
  wrote has no parts; then the order's own status is the shop's (specs/035 D3).
- **D2 - Staff see the whole order**, including the customer's address and every seller's parcel:
  staff are the shop, and the fulfilment queue already shows them every order's total.
- **D3 - Pay is confirmed in an AlertDialog** naming the shop and the amount the list showed. The
  server pays what is due *now*, which can be more if another parcel shipped meanwhile; the toast shows
  what was actually recorded.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | One read in Order, on Order's data. |
| II - Clean Architecture | Query in Application, route in WebApi. |
| III - Atomic writes | No new write. |
| IV - Identity from the token | Admin role from the token; the client's `isAdmin` only draws. |
| V - Evidence | Test: staff read any order; 403 for customer and seller in Bruno; client tests for queue, actions, payout confirm. |
| Schema compatibility | No migration. |

## Verification

Order tests; client lint/test/build; Bruno; `verify-saga.sh`; the console driven end to end in a browser.
