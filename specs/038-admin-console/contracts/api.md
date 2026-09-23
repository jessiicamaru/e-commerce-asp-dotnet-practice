# Contracts: An administrator's console

## New

### `GET /api/orders/fulfilment/{id}` (`Admin`)

Any order's detail - the same `OrderDetailResponse` its owner reads at `GET /api/orders/{id}`, including
`shipments[]` with `isShop`. **404** `Order not found.` when there is none; **403** for anyone else.

## Used as they are

- `GET /api/orders/fulfilment?status=Paid|Preparing|Shipped&page=&pageSize=` - the queue.
- `POST /api/orders/{id}/preparing`, `POST /api/orders/{id}/shipment` - the shop's parcel.
- `GET /api/orders/payouts/due`, `POST /api/orders/payouts` - settling sellers (specs/037).
