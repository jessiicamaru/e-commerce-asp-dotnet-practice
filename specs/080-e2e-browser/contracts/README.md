# Contracts: The storefront in a real browser

> Written on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

**No external interface changed.** No endpoint, gateway route, message, proto or storefront route was added,
removed or reshaped; `docs/reference/` did not need regenerating. The three storefront components the feature
changed (`ReviewForm`, the email editor and its versions, the notice-wording editor) send the same requests as
before - only when the success toast is shown changed (research D11).

What the suite **relies on** is below. It is written down because the suite is the check that fails when one of
these drifts: renaming any of them without updating `client/e2e/` turns a flow red, which is the point.

## Storefront routes the flows open

Served by the storefront container on `:8088` (`E2E_BASE_URL`), routes in `client/src/routes/index.tsx`.

| Route | Flow | What the flow does there |
| :-- | :-- | :-- |
| `/sign-in` | all four | Fills *Email* and *Password*, presses *Sign in* |
| `/admin/products` | 1 | Finds the waiting product (pages through with *Next*), presses *Approve* |
| `/products/:id` | 2, 4 | *Add to cart*; later *Review this product*, *5 stars*, *Post review* |
| `/cart` | 2 | Follows *Check out* |
| `/checkout` | 2 | Finds the saved address chosen, presses *Place order* |
| `/orders/:id` | 2, 4 | Waits for *Paid*; later *I've received it*, *Yes, it arrived* |
| `/shop/sales` | 3 | Follows the sale's *Placed ...* link |
| `/shop/sales/:id` | 3 | *Start preparing*, *Mark as shipped* with a tracking reference |

The words the flows wait for are the English bundle's, among them "“<name>” is on sale.", "Added 1 to your cart.",
"Paid. We will start preparing it soon.", "Marked as being prepared.", "Marked as shipped.", "Tracking reference:
...", "On its way.", "Thanks for confirming." and "Thank you - your review is up.".

## Gateway endpoints the setup calls

Through the same base URL, so through the storefront's nginx and then the gateway on `:5000`. All in
`client/e2e/support/api.ts`.

| Method and path | As | Expected |
| :-- | :-- | :-- |
| `POST /api/auth/login` | anyone | 200, `token` |
| `POST /api/auth/register` | anonymous | 200, `id` and `token` |
| `POST /api/auth/register-seller` | anonymous | 200 |
| `POST /api/auth/confirm-email` | anonymous | 204 |
| `GET /api/shop-applications/mine` | the seller | the pending application |
| `POST /api/shop-applications/{id}/approve` | administrator | 200 |
| `PUT /api/users/{id}/roles/Moderator` | administrator | a success status |
| `POST /api/categories` | administrator | 200 or 201, `id` |
| `POST /api/products` | the seller | 200, `id` and `variants[0].id` |
| `POST /api/products/{id}/approve` | administrator | 200 |
| `PUT /api/stock/{variantId}` | the seller | 200 (a 404 is retried while the stock row arrives) |
| `GET /api/products/{id}` | anonymous, then administrator | `availability`, `reviewStatus` |
| `POST /api/addresses` | the customer | 201, `id` |
| `DELETE /api/products/{id}`, `DELETE /api/categories/{id}` | administrator | not checked |

## Endpoints the pages call during the flows

The ones a broken route would break, from `client/src/services/`: `GET /api/products/review` and
`POST /api/products/{id}/approve` (flow 1); `GET /api/products/{id}`, `GET /api/stock/{id}`, `POST /api/cart/items`,
`GET /api/cart`, `GET /api/addresses`, `GET /api/orders/shipping-options`, `GET /api/orders/quote`,
`POST /api/orders`, `GET /api/orders/{id}` (flow 2); `GET /api/orders/sales`, `GET /api/orders/sales/{id}`,
`POST /api/orders/sales/{id}/preparing`, `POST /api/orders/sales/{id}/shipment` (flow 3);
`POST /api/orders/{id}/shipments/{shipmentId}/received`, `GET` and `PUT /api/products/{id}/reviews/mine`,
`GET /api/products/{id}/reviews` (flow 4). The negative control renamed the gateway's cart route, which the buying
flow's `POST /api/cart/items` and `GET /api/cart` go through.

## Outside the gateway

- **Mailpit's API**, `E2E_MAILPIT_URL` (default `http://localhost:8025`): `GET /api/v1/search?query=to:<email>` and
  `GET /api/v1/message/{ID}`, reading the `confirm-email?token=...` link from the message's text.
- **Environment**: `ADMIN_EMAIL`, `ADMIN_PASSWORD` (required), `E2E_BASE_URL`, `E2E_MAILPIT_URL`,
  `E2E_BROWSER_CHANNEL` (optional).
