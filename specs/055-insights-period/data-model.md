# Data Model: One insights period

> Written on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**No table, column, index or migration changed.** The feature changes how a period is resolved before the
existing queries run.

## `InsightsPeriod` (a value, not stored)

`readonly record struct InsightsPeriod(DateOnly FirstDay, DateOnly LastDay)` in
`server/src/BuildingBlocks/Ecommerce.Shared/Insights/InsightsPeriod.cs`.

| Member | Value |
| :--- | :--- |
| `MaxDays` | 366 |
| `DefaultDays` | 30 |
| `Start` | `FirstDay` 00:00 UTC |
| `End` | `LastDay + 1` 00:00 UTC, exclusive |
| `Days` | `LastDay − FirstDay + 1` |
| `Resolve(from, to, now)` | `LastDay` = day of `to ?? now`; `FirstDay` = day of `from`, or `LastDay − 29` |

## What each query reads with it

| Query | Service | Table | Bound used |
| :--- | :--- | :--- | :--- |
| Revenue | Order | `orders` (sold statuses), grouped by `CreatedAt` date and currency | `CreatedAt >= Start AND CreatedAt < End` |
| Top products | Order | `orders` × `order_items` | the same |
| Top buyers | Order | `orders` | the same |
| Top viewed | Catalog | `product_views` (one row per product per day) | `Day >= FirstDay AND Day <= LastDay` |

The Order bounds were already half-open; what changed is that they are now midnights. Catalog's bounds were
already whole days; what changed is that `from`/`to` are resolved by the shared rule (and the default is 30 days
including today rather than 30 days before `to`).
