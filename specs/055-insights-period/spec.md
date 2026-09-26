# Feature Specification: One insights period

> Completed on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature Branch**: `055-insights-period` | **Created**: 2026-09-24 | **Issue**: #125

**Status**: Merged (#138, 2026-09-24)

**Input**: Issue #125 - the four queries behind the administrator's Overview disagree about what a period is,
and the Overview's chart leaves out a day its totals count.

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

## User Scenarios & Testing *(mandatory)*

### US1 - Every panel covers the same days (Priority: P1)

An administrator picks "last 7 days". Revenue, top products, top buyers, top viewed and the chart all
cover exactly the same 7 UTC calendar days: today and the six before it.

**Why this priority**: It is the visible defect - totals that the chart beneath them does not add up to.

**Independent Test**: On the Overview choose "Last 7 days" and check that the request spans exactly the seven
days the chart draws; place orders at the edges of a day and check each query counts them.

**Acceptance Scenarios**:

1. The Overview asks for today and the N−1 days before it. The chart draws those N days, and every
   query answers for exactly those days.
2. An order placed at 00:30 on the first day counts, even when `from` names a later time that day. An
   order placed at 23:30 on the last day counts, even when `to` names an earlier time.

---

### US2 - One rule, enforced everywhere (Priority: P1)

Whatever panel or client asks, a period means the same thing and is refused for the same reasons.

**Why this priority**: Without one rule the next insight would pick its own, and #125 would recur; three of the
four queries also had no limit at all.

**Independent Test**: Ask each of the four endpoints for more than 366 days, and for a period that ends before it
starts.

**Acceptance Scenarios**:

1. A period is whole UTC days: from the day `from` falls on to the day `to` falls on, both included.
2. The default is the last 30 days, today included.
3. At most 366 days, and `from` not after `to` (a single day is allowed). All four queries answer 400
   otherwise, with the same messages.

### Edge Cases

- **A single day** (`from` and `to` on the same date, in any order of times): a valid one-day period.
- **`from` with no `to`**: the period runs to today. **Neither**: the last 30 days, today included.
- **A time with no time zone** (`DateTimeKind.Unspecified`): its date is taken as written; a UTC or local time is
  converted to UTC first.
- **Exactly 366 days**: allowed; 367 is refused.
- **An older client sending instants**: accepted; each is snapped to its day (FR-002).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `Ecommerce.Shared.Insights.InsightsPeriod` holds the rule: resolution, the day count and
  the validation. Order and Catalog both use it.
- **FR-002**: The parameters stay `DateTime`, so nothing breaks for Bruno or an older client. A time is
  simply snapped to its day.
- **FR-003**: The revenue response's `From` and `To` report the resolved whole-day bounds.
- **FR-004**: The refusals are one message each - "The period must not start after it ends." and "The period can
  be at most 366 days." - for all four queries.

### Key Entities

- **Insights period**: a first and a last UTC day, both included; `Start` is the first day's midnight, `End` the
  midnight after the last day (exclusive).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any choice of N on the Overview, the request spans exactly N calendar days and the chart draws
  exactly those N (the client test's `expected 8 to be 7` was the defect).
- **SC-002**: Orders at 00:30 on the first day and 23:30 on the last day are counted by revenue, top products and
  top buyers; views on both end days are counted by top viewed.
- **SC-003**: All four queries refuse 367 days and a reversed period with 400 and the same messages.

## Assumptions

- The Overview's days are UTC days, as revenue is grouped and views are counted by UTC day.

## Out of scope

- Which date a sale is dated by. At this merge revenue is dated by `orders.CreatedAt`; specs/072 later dated it by
  `PaidAt`.
- The seller's own insights page, which came later (specs/068) and uses the same rule.
