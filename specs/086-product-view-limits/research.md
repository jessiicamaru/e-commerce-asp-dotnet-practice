# Research: Honest view counts

> Written on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-27

Six decisions. D2, D4 and D6 are the spec's Decisions; D2 is also
[decisions.md row 67](../../docs/project/decisions.md). D1, D3 and D5 are read from the code and the pull request.

---

## D1 - Two guards, because neither alone is enough

**Decision**: a per-client rate limit at the gateway **and** once-per-viewer-per-day counting in Catalog.

**Rationale** (decisions row 67): the endpoint must stay anonymous and quiet (specs/047). A rate limit alone lets an
honest shopper's reloads count, up to the limit. Deduplication alone lets a script with a fresh id per call count
without bound. Together they cover the honest reload and the loop.

**Alternatives considered**:

- **Only a rate limit.** Rejected: reloads still inflate the count.
- **Only deduplication.** Rejected: a script defeats it.

---

## D2 - A random id kept by the browser, not the IP

**Decision**: the storefront's `visitorId()` keeps a `crypto.randomUUID()` in `localStorage` (`visitor-id`), reuses
a stored value only if it is a UUID, and returns nothing when storage throws. `Product.recordView` sends
`{ viewer: visitorId() }`.

**Rationale**: behind the storefront's nginx every browser shares one address unless the proxy is trusted, and an
IP is closer to personal data than a random id is. The id does nothing against a script; the rate limit does that.
With blocked storage a fresh id per call would dedupe nothing, so none is sent.

**Alternatives considered**:

- **A salted hash of the client IP.** Rejected (decisions row 67): shared behind nginx unless trusted, and closer to
  personal data.
- **A cookie set by the server.** Not recorded as considered.

---

## D3 - The token beats the body; everything is hashed

**Decision**: `ViewerOf` is `SHA256("user:{id}")` when `ICurrentUser.Id` is set, else `SHA256("visitor:{viewer}")`
when the body has one, else null; stored as `Convert.ToHexString` (64 characters).

**Rationale**: Constitution IV - a signed-in person is who their token says, and the body is something anybody
writes; the mutation "the body winning over the token" was run and caught. The prefixes keep a user id and a visitor
id that happen to be equal from colliding. The hash keeps the table from naming anybody.

**Alternatives considered**: storing the ids as they are - rejected (FR-005).

---

## D4 - Pruning inside the recording statement, not a sweeper

**Decision**: one statement:

```sql
WITH gone AS (DELETE FROM product_viewers WHERE "ProductId" = @p AND "Day" < @day),
     seen AS (INSERT INTO product_viewers ("ProductId", "Day", "Viewer") VALUES (@p, @day, @viewer)
              ON CONFLICT DO NOTHING RETURNING 1)
INSERT INTO product_views ("ProductId", "Day", "Views") SELECT @p, @day, 1 FROM seen
ON CONFLICT ("ProductId", "Day") DO UPDATE SET "Views" = product_views."Views" + 1;
```

**Rationale**: Catalog runs no hosted services. The insert is the claim - only the insert that succeeds feeds the
counter, so twenty at once from one viewer count once, with no read-then-write. The same statement deletes the
product's earlier days, so the table holds at most a day of viewers per product that was viewed.

**Alternatives considered**:

- **A sweeper deleting old rows.** Rejected: Catalog has no hosted service, and one would be a new moving part for a
  delete the write can do.
- **Read whether the viewer exists, then write.** Rejected: loses to concurrency, the defect specs/047's upsert was
  written to avoid.

---

## D5 - The limit is a route of its own at the gateway

**Decision**: `catalog-product-view-route` matches `POST /api/products/{id}/view` only, on the catalog cluster, with
`RateLimiterPolicy: "views"`; the catch-all products route keeps no policy. The limit is keyed by the client address
(`ClientKey`, IPv4-mapped addresses normalised), fixed window, no queue, counted in memory per gateway instance.

**Rationale**: reading products and every other product route must stay unlimited (FR-001); a separate, more
specific route is how YARP attaches a policy to one path and method. It reuses specs/062's machinery and its
trusted-proxy rule, so a forged `X-Forwarded-For` cannot spread one client over many keys.

**Alternatives considered**: a limit inside Catalog - not recorded as considered; it would need its own counter per
instance and would still answer 204.

---

## D6 - `AuthRateLimits` keeps its name

**Decision**: `views` is added to `AuthRateLimits` beside `sign-in`, `email` and `session`.

**Rationale**: renaming the class would touch the gateway's startup and its tests for no behaviour.

**Alternatives considered**: renaming to something like `RateLimits` - rejected for that reason.
