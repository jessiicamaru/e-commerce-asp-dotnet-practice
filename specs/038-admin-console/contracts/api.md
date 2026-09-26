# Contracts: An administrator's console

> Completed on 2026-09-27, after the feature merged (#82, #83), from the code at that merge, the pull
> requests and docs/features/fulfilment-and-delivery.md. HTTP only; no message or proto changed.

## New

### `GET /api/orders/fulfilment/{id}` (`Admin`)

Any order's detail - the same `OrderDetailResponse` its owner reads at `GET /api/orders/{id}`, including
`shipments[]` with `isShop`. **404** `Order not found.` when there is none; **403** for anyone else.

## Used as they are

- `GET /api/orders/fulfilment?status=Paid|Preparing|Shipped&page=&pageSize=` - the queue.
- `POST /api/orders/{id}/preparing`, `POST /api/orders/{id}/shipment` - the shop's parcel.
- `GET /api/orders/payouts/due`, `POST /api/orders/payouts` - settling sellers (specs/037).

## As built (from the code at #82)

`GET /api/orders/fulfilment/{id:guid}` - `OrdersController.GetForStaff`, `[Authorize(Roles = "Admin")]`,
through the gateway's existing `/api/orders/{**catch-all}` route.

| Situation | Answer |
| :-- | :-- |
| an administrator, the order exists | `200` `OrderDetailResponse` - lines, delivery address, totals, `shipments[]` with `sellerName` and `isShop` |
| an administrator, no such order | `404` `Order not found.` |
| a customer or a seller | `403` (Bruno: `security-checks/a customer cannot read an order as staff is 403`, `seller/a seller cannot read an order as staff is 403`) |
| no token | `401` |

⚠️ It is the one read of an order not scoped to its owner. The handler, `GetOrderForStaffQuery`, must not
be reused behind any route that is not Admin-only.

The console's calls, from `client/src/services/admin/index.ts`:

| Method | Path | Body |
| :-- | :-- | :-- |
| `GET` | `/api/orders/fulfilment?status={Paid\|Preparing\|Shipped}&page=&pageSize=` | - |
| `GET` | `/api/orders/fulfilment/{id}` | - |
| `POST` | `/api/orders/{id}/preparing` | - |
| `POST` | `/api/orders/{id}/shipment` | `{ "trackingReference": "…" }` |
| `GET` | `/api/orders/payouts/due` | - |
| `POST` | `/api/orders/payouts` | `{ "sellerId": "…", "currency": "VND" }` - **no amount** |

## Client routes

`/admin` (the queue, its state in the URL), `/admin/orders/:id`, `/admin/payouts`, all under
`RequireRole role="Admin"` inside `AdminLayout`. `RequireRole` draws; the server decides.
