# Feature Specification: One insights period

**Feature Branch**: `055-insights-period` | **Created**: 2026-09-24 | **Issue**: #125

## Why

The administrator's Overview (specs/047) is composed from four queries. They did not agree on what a
period is:

| Query | Limit | Ends |
| :-- | :-- | :-- |
| Order revenue | 366 days | `from` inclusive, `to` exclusive, to the instant |
| Order top products | none | the same |
| Order top buyers | none | the same |
| Catalog top viewed | none | whole days, both included |

The Overview also asked for `from` = now minus N days, which touches **N+1** calendar days, while its
chart draws N. So the earliest, partial day's revenue was counted in the totals and had no bar. The
totals and the chart did not add up to each other.

## User Scenarios

### US1 - Every panel covers the same days (P1)

An administrator picks "last 7 days". Revenue, top products, top buyers, top viewed and the chart all
cover exactly the same 7 UTC calendar days: today and the six before it.

**Acceptance**
1. The Overview asks for today and the N−1 days before it. The chart draws those N days, and every
   query answers for exactly those days.
2. An order placed at 00:30 on the first day counts, even when `from` names a later time that day. An
   order placed at 23:30 on the last day counts, even when `to` names an earlier time.

### US2 - One rule, enforced everywhere (P1)

**Acceptance**
1. A period is whole UTC days: from the day `from` falls on to the day `to` falls on, both included.
2. The default is the last 30 days, today included.
3. At most 366 days, and `from` not after `to` (a single day is allowed). All four queries answer 400
   otherwise, with the same messages.

## Requirements

- **FR-001**: `Ecommerce.Shared.Insights.InsightsPeriod` holds the rule: resolution, the day count and
  the validation. Order and Catalog both use it.
- **FR-002**: The parameters stay `DateTime`, so nothing breaks for Bruno or an older client. A time is
  simply snapped to its day.
- **FR-003**: The revenue response's `From` and `To` report the resolved whole-day bounds.
