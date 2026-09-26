# Research: One insights period

> Written on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

The pull request records D1 and D2 as decided on the user's behalf.

---

## D1 - Whole UTC days, both ends included

**Decision**: A period is the day `from` falls on to the day `to` falls on, both included. For Order's SQL that is
`t >= Start && t < End`, with `End` the midnight after the last day.

**Rationale**: Revenue is grouped by day, views are counted by day and the chart is drawn by day. A cut at an
instant always leaves one bar that counts part of a day. Catalog's top viewed was already whole days, inclusive.

**Alternatives considered**:

- **Instants, as Order had.** Rejected for the reason above: it is how the partial first day ended up in the
  totals with no bar.

---

## D2 - The rule lives in `Ecommerce.Shared`

**Decision**: `Ecommerce.Shared.Insights.InsightsPeriod` and `InsightsPeriodRules.ValidPeriod`, used by Order and
Catalog.

**Rationale**: Like `StaffRoles`: the rule is shared; each service still answers from its own data. A copy per
service is how the four queries came to disagree.

**Alternatives considered**: none recorded beyond keeping per-service rules, which is the state #125 reports.

---

## D3 - Parameters stay `DateTime`; a time is snapped to its day

**Decision**: `From` and `To` remain `DateTime?` on every query; `Resolve` takes the date of each (converting a
UTC or local value to UTC first; an unspecified kind is taken as written).

**Rationale**: Nothing breaks for Bruno or an older client that sends instants.

**Alternatives considered**:

- **`DateOnly` parameters.** Not recorded as considered; FR-002 rules it out as a breaking change.

---

## D4 - The client asks for N days, not N×24 hours

**Decision**: The Overview's `from` is now minus `(period - 1) × 24 h`, `to` is now; the server snaps both to their
days.

**Rationale**: Now minus N×24 h touches N+1 dates. With the server counting whole days, `(N − 1)` days back is
exactly the N days `periodDays(to, N)` draws.

**Alternatives considered**: none recorded.
