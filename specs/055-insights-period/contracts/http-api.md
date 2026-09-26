# HTTP Contract: One insights period

> Written on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](../spec.md)

Four existing endpoints change what their period parameters mean and gain the same refusals. No route, parameter
name or type changed; no message or gRPC contract changed.

## Endpoints - all `Admin` only, through the gateway on :5000

| Method and path | Service | Query parameters |
| :--- | :--- | :--- |
| `GET /api/orders/insights/revenue` | Order | `from`, `to` |
| `GET /api/orders/insights/top-products` | Order | `from`, `to`, `by` (`units`/`revenue`), `currency`, `limit` (1-50) |
| `GET /api/orders/insights/top-buyers` | Order | `from`, `to`, `currency`, `limit` (1-50) |
| `GET /api/products/insights/top-viewed` | Catalog | `from`, `to`, `limit` (1-50) |

## The period parameters

`from` and `to` are optional ISO 8601 date-times (`DateTime?`, unchanged). Since this feature:

- The period is **whole UTC days**: the day `from` falls on to the day `to` falls on, **both included**. The time
  of day is ignored.
- `to` omitted: today. `from` omitted: 29 days before the last day (30 days, today included, by default).
- A single day (`from` and `to` on one date) is valid.

## Refusals (new on top products, top buyers and top viewed; reworded on revenue)

`400` ProblemDetails with `errors`. The rule is named `Period` (`.WithName("Period")`); the key the shared handler
writes it under is shown here as `Period` but was not checked against a live answer for this record:

```json
{
  "status": 400,
  "title": "Validation Failed",
  "errors": { "Period": ["The period can be at most 366 days."] }
}
```

| Condition | Message |
| :--- | :--- |
| The first day is after the last | `The period must not start after it ends.` |
| More than 366 days | `The period can be at most 366 days.` |

Before this feature revenue said "The period must start before it ends." and alone had the 366-day limit; the
other three had no limit.

## Revenue response

```json
{ "from": "2026-09-18T00:00:00Z", "to": "2026-09-25T00:00:00Z", "totals": [ ... ], "days": [ ... ] }
```

`from` and `to` now report the resolved bounds: the first day's midnight and, **exclusive**, the midnight after
the last day (FR-003). The dates above are an illustration of a 7-day period ending 2026-09-24.

## Bruno

`bruno/admin-insights/a period longer than 366 days is refused.yml` - `GET
/api/orders/insights/top-products?from=2024-01-01T00:00:00Z&to=2025-12-31T00:00:00Z` as the administrator → 400
whose body contains "at most 366 days".
