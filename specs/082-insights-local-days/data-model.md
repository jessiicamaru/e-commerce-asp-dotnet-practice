# Data Model: Insights in the shop's days

> Written on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration changed.** What changed is how existing columns are read, and what one
existing column's value means from the merge on.

---

## `orders` (Order) - read differently

| Column | Type | How it is read now |
| :--- | :--- | :--- |
| `PaidAt` | `timestamp with time zone`, nullable (specs/072) | `(PaidAt ?? CreatedAt) AT TIME ZONE '<zone>'`, then its date, is the day an order counts on |
| `CreatedAt` | `timestamp with time zone` | The fallback for orders from before specs/072 |

The period filter is `(PaidAt ?? CreatedAt) >= Start AND < End`, where `Start` and `End` are the shop's midnights
as UTC instants. Top products and top buyers use the same bounds and do not group by day.

## `product_views` (Catalog) - the meaning of `Day`

| Column | Type | Meaning before | Meaning after the merge |
| :--- | :--- | :--- | :--- |
| `ProductId` | `uuid`, PK part | unchanged | unchanged |
| `Day` | `date`, PK part, indexed | the UTC date of the view | the date of the view **in the shop's zone** |
| `Views` | `integer` | unchanged | unchanged |

Rows written before the merge keep their UTC day; there is no hour to move them by (research D4).

## Values, not rows

| Value | Where | Before | After |
| :--- | :--- | :--- | :--- |
| `InsightsPeriod.Start` | Shared | first day at 00:00 UTC | first day at 00:00 in the zone, as UTC (Hanoi: 17:00 UTC the day before) |
| `InsightsPeriod.End` (exclusive) | Shared | the day after the last, 00:00 UTC | the day after the last, 00:00 in the zone, as UTC |
| `RevenueResponse.FirstDay` / `LastDay` | Order | absent | `DateOnly`, the period's first and last shop day |

`Start` and `End` became record fields computed by `Resolve` rather than properties derived from the days, because
turning a day into an instant now needs the calendar.

## Configuration

| Key | Default | Read by | On a bad value |
| :--- | :--- | :--- | :--- |
| `Insights:TimeZone` | `Asia/Ho_Chi_Minh` | Order and Catalog, at startup (`AddInsightsCalendar`) | The service does not start |

It is not set in any `appsettings.json` or compose file at the merge; the default applies.

## Schema evolution

Nothing to evolve. An earlier Order image running against the same database counts UTC days again, and an earlier
Catalog image records views on UTC days again; neither fails.
