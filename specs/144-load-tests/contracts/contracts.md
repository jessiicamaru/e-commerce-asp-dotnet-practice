# Contracts: Load-test checkout and measure it

No HTTP, message or gRPC shape changes. The scenarios use existing endpoints only:

| Use | Endpoint | Through |
| :-- | :-- | :-- |
| Staff sign-in | `POST /api/auth/login`, then the two-factor step with the back office's `Origin` | gateway |
| Setup | `POST /api/categories`, `POST /api/products`, `PUT /api/stock/{variantId}` | gateway |
| Customers | `POST /api/auth/register`, `POST /api/addresses` | **Identity directly** (D2) |
| Browse | `GET /api/products`, `GET /api/products?search=`, `GET /api/products/{id}` | gateway |
| Checkout | `POST /api/cart/items`, `GET /api/orders/quote`, `POST /api/orders` | gateway |
| Settle and check | `GET /api/orders/{id}`, `GET /api/stock/{variantId}` | gateway |
| Cleanup | `DELETE /api/products/{id}`, `DELETE /api/categories/{id}` | gateway |

## `run.sh`

```text
server/loadtest/run.sh browse|checkout|race [k6 options...]
```

Reads `ADMIN_EMAIL`, `ADMIN_PASSWORD` and `ADMIN_TOTP_SECRET` from the environment or `server/.env`. Exit 0 when every
threshold held; k6's exit code otherwise.
