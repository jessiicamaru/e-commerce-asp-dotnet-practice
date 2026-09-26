# Feature Specification: Insights in the shop's days

**Feature Branch**: `082-insights-local-days` | **Created**: 2026-09-26 | **Issue**: #168 (closes it)

## Why

Every insight - revenue per day, top selling, top buyers, most viewed, a seller's own - counted **UTC days**
(specs/055). The shop's customers are in Vietnam (UTC+7), so for the first seven hours of every local day an order
counted on the day before: one paid at 06:30 in Hanoi was yesterday's revenue, and "today" on the chart did not
begin until 07:00. The storefront worked out the chart's days from the browser's clock in UTC too.

## Requirements

- **FR-001** The shop has **one time zone**, `Insights:TimeZone`, an IANA id with `Asia/Ho_Chi_Minh` as the
  default. An id the machine does not know stops the service at startup rather than counting in UTC.
- **FR-002** Every insight counts **that zone's days**:
  - the period's ends: a time snaps to its day in the shop's zone, and a bare date is that day;
  - Order's grouping of revenue per day, the shop's and a seller's;
  - Catalog's view counter, which records a view on the shop's today.
- **FR-003** The revenue response names the days it covered, `firstDay` and `lastDay`, and the chart draws
  exactly those. The browser's clock knows neither the shop's zone nor what the server counted.
- **FR-004** The period rules of specs/055 are unchanged: whole days, both ends included, at most 366, one rule
  and the same words for every insight.

## Decisions

- **The shop's zone, not the reader's.** Two administrators in two countries must see the same numbers, and a
  seller's day is the shop's day. A per-reader zone would make every total depend on who asks.
- **Grouped in PostgreSQL.** `TimeZoneInfo.ConvertTimeBySystemTimeZoneId(x, zone)` is what Npgsql translates to
  `AT TIME ZONE`; `EF.Functions.AtTimeZone` has no `DateTime` overload. The zone id handed to the database is IANA,
  converted from a Windows id if the machine gave one.
- **No migration.** Orders are dated by instants (`PaidAt ?? CreatedAt`), which regroup by themselves. Views are
  stored per day, so views recorded before this keep their UTC day - a known limit, not worth inventing hours for.
- **`firstDay`/`lastDay` are additions.** A storefront older than this ignores them. A storefront newer than an
  Order without them falls back to the dates of the instants it asked for, which is the old behaviour.

## Out of scope

- A time zone per seller, or per reader.
- Moving existing `product_views` rows.
