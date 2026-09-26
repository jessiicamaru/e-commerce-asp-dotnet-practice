# HTTP Contract: A Storefront That Builds and Reaches the Gateway

> Written on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull request
> and docs/architecture/storefront.md (the storefront has no page of its own under docs/features/).

**Feature**: [spec.md](../spec.md)

**No API changed.** The feature adds a consumer, not an endpoint. This file records the interfaces the
client relies on, so a later change to either side can be checked against them.

## The dev proxy (the client's side of the boundary)

| Browser asks | Vite (`:5173`) forwards to | Notes |
| :--- | :--- | :--- |
| `/api/**` | `${GATEWAY_URL:-http://localhost:5000}/api/**` | `changeOrigin: false`; same origin, so no CORS and the refresh cookie is first-party |
| anything else | served by Vite (the SPA) | client-side routes such as `/status` |

## Endpoints called at this merge

All anonymous, all through the gateway's existing health routes, which rewrite `/api/<svc>/health` to
the service's `/health`:

| Request | Expected | Shown as |
| :--- | :--- | :--- |
| `GET /api/identity/health` | 200 | up |
| `GET /api/catalog/health` | 200 | up |
| `GET /api/order/health` | 200 | up |
| `GET /api/inventory/health` | 200 | up |
| `GET /api/payment/health` | 200 | up |
| `GET /api/cart/health` | 200 | up |

Any other status is shown as `down (<status>)`; a network error as `down (<error>)`. The orchestrator
has no route (no HTTP surface at the time).

## What every later call assumes (`src/api/http.ts`)

- **Errors are RFC 7807 ProblemDetails** from the shared `GlobalExceptionHandler`, with validation
  failures under `errors: { "<Field>": ["message", ...] }`. A service that answered an error in any
  other shape would surface as an `ApiError` with only a status.
- **A 401 means the access token expired or is missing**, and one refresh may fix it. (The refresh
  endpoint itself is wired in #35.)
- **A 204 has no body.**
- **Credentials are sent** (`credentials: 'include'`) so the HttpOnly refresh cookie reaches Identity.
