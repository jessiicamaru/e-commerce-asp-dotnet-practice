# HTTP contract: my data

Every route is `GET`, requires a signed-in caller (401 otherwise), and answers the caller's own data from the token. All
of them go through existing gateway catch-all routes, so no new gateway route is needed.

| Service | Route |
| :-- | :-- |
| Identity | `/api/auth/me/data` |
| Catalog | `/api/products/my-data` |
| Order | `/api/orders/my-data` |
| Cart | `/api/cart/my-data` |
| Payment | `/api/payments/my-data` |
| Activity | `/api/notifications/my-data` |

## Shape (every service)

```json
{
  "service": "identity",
  "exportedAt": "2026-10-01T08:00:00Z",
  "sections": {
    "profile": [ { "email": "…", "firstName": "…" } ],
    "addresses": [ … ]
  },
  "withheld": [
    { "table": "refresh_tokens", "reason": "The secrets of your sessions. Sign out to end them." }
  ]
}
```

Each section is an array, even when it holds a single row, so every section reads the same way.

## The file the storefront writes

```json
{
  "exportedAt": "…",
  "person": { "id": "…", "email": "…" },
  "services": {
    "identity": { …the answer above… },
    "catalog": { … },
    "payment": { "unavailable": true }
  }
}
```
