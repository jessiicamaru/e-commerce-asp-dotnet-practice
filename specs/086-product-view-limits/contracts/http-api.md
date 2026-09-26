# HTTP Contract: Honest view counts

> Written on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](../spec.md)

One endpoint gained an optional body, and the gateway gained a route with a rate limit for it. No message or gRPC
contract changed.

---

## `POST /api/products/{id}/view` - anonymous

Body, **optional** (`EmptyBodyBehavior.Allow`; an older storefront sends none):

```json
{ "viewer": "3f2b8c1e-5d4a-4e6f-9a7b-1c2d3e4f5a6b" }
```

| Field | Type | Meaning |
| :--- | :--- | :--- |
| `viewer` | UUID, optional | The browser's visitor id. **Ignored when the request carries a valid token**: a signed-in person is their token's id |

**Response**: always `204 No Content`, counted or not - unchanged from specs/047, so the answer says nothing about
the product or about whether the view counted. A body that is not valid JSON for this shape is refused by model
binding like any other endpoint's (not specific to this feature).

## Gateway

Route `catalog-product-view-route` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`:

```json
"catalog-product-view-route": {
  "ClusterId": "catalog-cluster",
  "RateLimiterPolicy": "views",
  "Match": { "Path": "/api/products/{id}/view", "Methods": [ "POST" ] }
}
```

Policy `views`: fixed window, **30 calls per 60 seconds per client address**, no queue;
`RateLimits:views:PermitLimit` and `RateLimits:views:WindowSeconds` override it, and a value below 1 stops the
gateway at startup. The client address is the connection's, or `X-Forwarded-For` only from a proxy listed in
`GATEWAY_TRUSTED_PROXIES` (specs/062). One allowance covers every product.

**Over the limit** - `429 Too Many Requests`, `Retry-After: <seconds>`, `application/problem+json`:

```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many attempts. Try again in 42 seconds.",
  "instance": "/api/products/0193.../view",
  "retryAfter": 42
}
```

The storefront calls the endpoint as `Product.recordView(id).catch(() => {})`, so a 429 is invisible to a shopper.

**Not limited**: `GET /api/products`, `GET /api/products/{id}`, `/api/products/{id}/saved` and every other product
route - they stay on `catalog-products-route`, which has no policy.
