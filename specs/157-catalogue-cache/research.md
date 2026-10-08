# Research: The catalogue's public reads, served from memory

## D1. Output caching, not HybridCache around the handlers

**Decision**: ASP.NET Core output caching (`AddOutputCache` / `[OutputCache(PolicyName = "catalogue")]`) on the four
public GETs.

**Rationale**:
- It caches the **whole HTTP answer** - body and the headers that say what it is (`Content-Language`, `X-Currency`,
  `Vary`) - so a replay cannot disagree with the original.
- Its default policy already refuses what must not be cached: requests with `Authorization`, non-GET, non-200,
  responses setting a cookie.
- Tag eviction (`IOutputCacheStore.EvictByTagAsync`) is built in, and the store is pluggable (a Redis store exists),
  which is the user's "Redis later" without touching the endpoints.

**Alternatives rejected**:
- *HybridCache in a MediatR behavior*: caches the response DTO, not the answer; every cached query needs a key that
  remembers language, currency and caller, and serialising the DTOs (records, paging types) is one more thing that can
  differ from what the controller returns.
- *HTTP `Cache-Control` for browsers/proxies*: would let a browser keep a taken-down product with no way to evict it.
- *Redis now*: one more piece of infrastructure (compose, production, health, docs) for one instance per service.

## D2. What varies

The query string (all keys), the **negotiated** language (`IRequestLanguage.Current`) and the **negotiated** currency
(`IRequestCurrency.Current`) - not the raw `Accept-Language`, which would split `en-GB`, `en;q=0.9,vi;q=0.8` and `en`
into three entries of one answer.

## D3. Eviction: any committed write to the catalogue's tables

**Decision**: an EF Core interceptor on `CatalogDbContext` (`CatalogueWrites`, Infrastructure) that recognises a
command writing one of the catalogue's tables (`INSERT INTO` / `UPDATE` / `DELETE FROM` / `ON CONFLICT ... UPDATE` on
`products`, `product_variants`, `variant_prices`, `product_translations`, `variant_options`,
`variant_option_translations`, `categories`, `category_translations`, `sellers`) and evicts the `catalogue` tag:
- inside a transaction: after `TransactionCommitted`; nothing on rollback;
- outside one: after the command.

**Rationale**:
- Catalog writes those tables from ~40 command handlers, a dozen consumers and many guarded SQL statements
  (`ExecuteSqlRaw`, CTEs). An eviction call per handler would be missed by the next handler written; the interceptor
  sees every statement whichever path sent it.
- Matching tables by name with word boundaries keeps out `product_views`, `product_viewers`, `saved_products`,
  `product_questions`, `review_eligibility`, the outbox and the inbox - a view is counted on every product page, and
  evicting on it would empty the cache all the time.
- Everything at once (one tag): writes are rare, and a finer key would have to know which listing a price change
  reaches.

**Alternatives rejected**: eviction calls in handlers (misses a path); `SaveChanges` interception only (misses every
guarded SQL statement); a short expiry alone (a taken-down product would stay up to it).

## D4. Expiry

`Caching:CatalogueSeconds`, 30 by default. It bounds what eviction cannot reach: another instance's memory and a read
that raced a commit (D3's eviction happens after the commit; a read that began before it can store the old answer just
after). 0 switches caching off; anything outside 0-600 stops Catalog at start, like its other required settings.

## D5. How it is judged

- An HTTP test (TestServer) through the same registration Program.cs uses: hits counted by a handler.
- Interceptor tests on PostgreSQL through `CatalogDbContext`: committed EF write, committed raw SQL, rollback, view.
- `loadtest/run.sh browse` several warm runs before and after, on the same machine (CLAUDE.md: never one run, never
  right after a rebuild).
