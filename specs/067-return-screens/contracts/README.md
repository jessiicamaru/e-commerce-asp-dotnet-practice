# Contracts: Returning a delivered parcel (part 2 - the screens)

> Written on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**This feature changed no external interface** - no endpoint, message, table or gateway route. It relies on the HTTP
contract of specs/066, [../066-parcel-returns/contracts/http-api.md](../../066-parcel-returns/contracts/http-api.md),
exactly as merged in #149. The reference was regenerated at this merge with no change (124 endpoints, 24 messages,
39 tables).

## What the storefront calls

All through `http` (axios, base `/api`), so through the Vite proxy or nginx to the gateway on :5000.

| Service method | Request | Used by |
| :-- | :-- | :-- |
| `Order.requestReturn(orderId, shipmentId, reason)` | `POST /api/orders/{orderId}/shipments/{shipmentId}/return` `{ reason }` | buyer, `parcel-return` |
| `Order.escalateReturn(orderId, shipmentId)` | `POST /api/orders/{orderId}/shipments/{shipmentId}/return/escalate` | buyer |
| `Order.sendReturnBack(orderId, shipmentId, trackingReference)` | `POST /api/orders/{orderId}/shipments/{shipmentId}/return/sent` `{ trackingReference }` | buyer |
| `Order.acceptSaleReturn(id)` | `POST /api/orders/sales/{id}/return/accept` | seller, `pages/shop-sale` |
| `Order.refuseSaleReturn(id, reason)` | `POST /api/orders/sales/{id}/return/refuse` `{ reason }` | seller |
| `Order.receiveSaleReturn(id)` | `POST /api/orders/sales/{id}/return/received` | seller |
| `Admin.returns(status, page, pageSize)` | `GET /api/orders/returns?status=&page=&pageSize=` | `pages/admin-returns` |
| `Admin.acceptReturn(orderId, shipmentId)` | `POST /api/orders/fulfilment/{orderId}/shipments/{shipmentId}/return/accept` | staff, `pages/admin-order` |
| `Admin.refuseReturn(orderId, shipmentId, reason)` | `POST /api/orders/fulfilment/{orderId}/shipments/{shipmentId}/return/refuse` `{ reason }` | staff |
| `Admin.receiveReturn(orderId, shipmentId)` | `POST /api/orders/fulfilment/{orderId}/shipments/{shipmentId}/return/received` | staff |

Every call answers the `ParcelReturn` as it now stands (or a `ReturnPage`), which the hooks write into the query cache
of the order, the sale, or the queue. A 409 is shown in the server's words (the ProblemDetails `detail`).

Reads unchanged in shape but now drawn: `GET /api/orders/{id}` (`shipments[].return`), `GET /api/orders/sales/{id}`
(`return`), `GET /api/orders/fulfilment/{id}` (`shipments[].return`).

The five notice kinds from specs/066 were already worded in #149; nothing about notices changed here.
