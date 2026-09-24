# Implementation Plan: One insights period

**Branch**: `055-insights-period` | **Spec**: [spec.md](spec.md) | **Issue**: #125

## Design

`InsightsPeriod(DateOnly FirstDay, DateOnly LastDay)` lives in `Ecommerce.Shared/Insights`.

| Member | Meaning |
| :-- | :-- |
| `Start` | `FirstDay` at 00:00 UTC |
| `End` | **exclusive**: the day after `LastDay`, at 00:00 UTC |
| `Days` | the number of days, both ends included |
| `Resolve(from, to, now)` | the period a request names |
| `ValidPeriod(x => x.From, x => x.To)` | a FluentValidation rule builder, used by all four validators |

- **Order:** the repository queries keep `>= from && < to`, fed `Start` and `End`. The revenue response
  reports `Start` and `End`.
- **Catalog:** top viewed is fed `FirstDay` and `LastDay`, and was already inclusive.
- **Client:** `from` = today minus (N−1) days and `to` = now. `periodDays(to, N)` then draws exactly the
  days asked for.

**Why whole days.** Revenue is grouped by day, views are counted by day, and the chart is drawn by day.
A period cut mid-day always leaves one bar that counts part of a day.

## Constitution check

- I (autonomy): each service still answers from its own data. Only the rule is shared, the way
  `StaffRoles` is. Pass.
- V (evidence): the limit test fails first on top products, top buyers and top viewed. The snapping test
  fails first at both ends. The client test fails first on the N+1 request. Pass.
