# HTTP contract: shop pause and closure

All routes are in Catalog, through the gateway's existing `/api/shops/{**catch-all}` route. No new gateway route is
needed.

## The state

```json
{
  "sellerId": "0199...",
  "shopName": "Lens Corner",
  "state": "Open | Paused | Closed | Suspended",
  "pausedAt": "2026-09-27T08:00:00Z",
  "closedAt": null,
  "closedReason": null
}
```

`state` is worded by precedence: Suspended, then Closed, then Paused, then Open. `pausedAt` and `closedAt` are given
whatever the precedence, so a shop both closed and paused shows both.

## Seller

| Method | Path | Body | Answers |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/shops/mine` | - | 200 state; 404 `Shop not found.` |
| `POST` | `/api/shops/mine/pause` | - | 200 state; 404; 409 `Staff closed this shop: ...` / `The shop is already paused.` |
| `POST` | `/api/shops/mine/reopen` | - | 200 state; 404; 409 closed / `The shop is not paused.` |

## Staff (Admin, Moderator)

| Method | Path | Body | Answers |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/shops/closed?pageNumber=&pageSize=` | - | 200 `{ items: [state], totalCount }`, newest closure first |
| `POST` | `/api/shops/{sellerId}/close` | `{ "reason": "..." }` (1-500) | 200 state; 400; 404; 409 `The shop is already closed.` |
| `POST` | `/api/shops/{sellerId}/reopen` | - | 200 state; 404; 409 `The shop is not closed.` |

A customer or a seller calling a staff route gets 403. Anonymous callers get 401 on every route above.

## Public (changed)

`GET /api/shops/{sellerId}` adds `"paused": true|false`. A closed shop is a 404, like a suspended one.

## Notices (`notification-kinds.json`)

| Kind | Data | To |
| :-- | :-- | :-- |
| `ShopClosed` | `reason` | the seller |
| `ShopReopened` | - | the seller |
