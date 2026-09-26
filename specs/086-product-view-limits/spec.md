# Feature Specification: Honest view counts

**Feature Branch**: `086-product-view-limits` | **Created**: 2026-09-27 | **Issue**: #173 (closes it)

## Why

`POST /api/products/{id}/view` is anonymous and always answers 204 (specs/047), and every call added one view.
Nothing limited how often it was called. A reload, or a loop, pushed a product up the administrator's "most
viewed" list and a seller's own views at will.

## Requirements

- **FR-001** The gateway limits the view route per client IP with its own policy, `views`: 30 calls a minute,
  configurable as `RateLimits:views:*`. It uses the specs/062 machinery and the same trusted-proxy rule. Reading
  products and every other product route stay unlimited.
- **FR-002** A view counts **once a day per viewer**.
  - A signed-in person is identified by their token's id, whatever the body says (Constitution IV).
  - A visitor is identified by a random id the storefront keeps in the browser, sent as `{ "viewer": "<uuid>" }`.
  - A call that names nobody still counts. That covers an older storefront and blocked storage, and the gateway
    limit bounds it.
- **FR-003** It stays a single statement with no read-then-write: the insert into `product_viewers` is the claim,
  and only when it succeeds does `product_views` increment. Twenty calls at once from one viewer count once.
- **FR-004** The endpoint stays anonymous and always 204 (specs/047).
- **FR-005** Nothing names a person:
  - the viewer is stored only as a SHA-256;
  - only today's rows are kept, because the product's first view of a day deletes its earlier rows in the same
    statement.

## Decisions

- **A random id kept by the browser, not the IP.** Behind the storefront's nginx every browser shares one
  address unless the proxy is trusted, and an IP is closer to personal data than a random id is. The id does
  nothing against a script that sends a new id each time; the rate limit is what stops that. The id is there so
  that an honest reload does not count again.
- **Pruning inside the recording statement, not a sweeper.** Catalog runs no hosted services. A data-modifying
  CTE deletes the product's older rows as it records the new one, so the table holds at most a day of viewers per
  product that was viewed.
- **The class keeps its name, `AuthRateLimits`**, although `views` is not about authentication. Renaming it would
  touch the gateway's startup and its tests for no behaviour.

## Out of scope

- Telling bots from people.
