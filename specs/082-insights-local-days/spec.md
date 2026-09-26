# Feature Specification: Insights in the shop's days

> Completed on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature Branch**: `082-insights-local-days` | **Created**: 2026-09-26 | **Status**: Merged (#170, 2026-09-26) | **Issue**: #168 (closes it)

**Input**: Issue #168 - an order paid in the Hanoi morning counted as the previous day's revenue.

## Why

Every insight - revenue per day, top selling, top buyers, most viewed, a seller's own - counted **UTC days**
(specs/055). The shop's customers are in Vietnam (UTC+7), so for the first seven hours of every local day an order
counted on the day before: one paid at 06:30 in Hanoi was yesterday's revenue, and "today" on the chart did not
begin until 07:00. The storefront worked out the chart's days from the browser's clock in UTC too.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A morning sale counts on its own morning (Priority: P1)

An administrator opens the Overview for today at 08:00 in Hanoi. The order a customer paid at 06:30 is in today's
revenue, not yesterday's; a period of "one day" means the Hanoi day.

**Why this priority**: This is the issue. With UTC days nearly a third of every local day was filed under the day
before, so every daily figure was wrong in a way nobody could see from the page.

**Independent Test**: Place an order paid at 23:30 UTC; ask for revenue of the next day in Hanoi; the order is
there, on that day, and in the period's totals.

**Acceptance Scenarios**:

1. **Given** an order paid at 23:30 UTC on day D-1, **When** revenue is asked for day D (Hanoi), **Then** the one
   day in the answer is D and the total is that order's.
2. **Given** a request whose `from` is a time, **When** the period is resolved, **Then** it starts at midnight of
   that time's day in Hanoi (17:00 UTC the day before) and `firstDay` names that day.
3. **Given** a bare date `2026-09-26` in the query, **When** the period is resolved, **Then** it is taken as that
   day of the shop, not converted.

---

### User Story 2 - The chart draws the days the server counted (Priority: P2)

The revenue chart's columns are exactly the days the server counted - the first to the last, both included - not
days the browser worked out from its own clock in UTC.

**Why this priority**: Without it, a correct server still produced a chart shifted by a day for seven hours of
every day, and the fix would look like it had not worked. It is second because it only matters once the server
counts the right days.

**Independent Test**: Give the Overview a revenue response whose `firstDay`..`lastDay` are far from today; the
chart draws exactly those days.

**Acceptance Scenarios**:

1. **Given** a response with `firstDay = 2026-09-22` and `lastDay = 2026-09-24`, **When** the chart is drawn,
   **Then** it has three columns, 22, 23 and 24.
2. **Given** a response from an Order older than this feature (no `firstDay`), **When** the chart is drawn,
   **Then** it falls back to the dates of the instants it asked for, the old behaviour.
3. **Given** ends the wrong way round or unreadable, **When** the chart is drawn, **Then** it draws nothing rather
   than throwing.

---

### User Story 3 - Views and a seller's figures use the same days (Priority: P3)

A product view is recorded on the shop's today, and a seller's own revenue, top products and views count the same
days as the administrator's.

**Why this priority**: One definition of a day across every insight, or two pages disagree about the same order.
Third because views are a smaller number than money.

**Independent Test**: Ask a seller's revenue for the last 30 days; `lastDay - firstDay + 1` is 30 and `from` is
midnight in Hanoi.

**Acceptance Scenarios**:

1. **Given** a view at 23:30 UTC, **When** it is recorded, **Then** it counts on the next day's row in
   `product_views`.
2. **Given** a seller's revenue with no period, **When** it is answered, **Then** it covers the last 30 shop days,
   today included, and names them.

---

### Edge Cases

- **Two instants on one Hanoi day that straddle a UTC midnight.** Counted in UTC days `from` would fall after `to`
  and the validator would refuse a valid period; the validator resolves the period in the shop's days too.
- **A zone id the machine does not know.** The service does not start, rather than counting in UTC.
- **A Windows machine.** A Windows zone id is converted to its IANA id before it is handed to PostgreSQL.
- **Views recorded before this feature.** They keep their UTC day: the rows hold a day, not an instant. A known
  limit.
- **Daylight saving.** Asia/Ho_Chi_Minh has none; `StartOf` converts through `TimeZoneInfo`, so a zone that has it
  would still get its own midnight. Not exercised by a test.
- **An older storefront and a newer Order.** The storefront ignores `firstDay`/`lastDay` and draws as before.

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-005** The revenue response's `from` and `to` still report the period's bounds as UTC instants, `to`
  exclusive; they are now the shop's midnights.

### Key Entities

- **Shop calendar** (`InsightsCalendar`): the configured zone, which day an instant falls on, and when a day
  begins.
- **Insights period** (`InsightsPeriod`): first and last day, both included, and the UTC instants that bound them.
- **Revenue response**: gains `firstDay` and `lastDay`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An order paid at 23:30 UTC counts on the next day in Hanoi - in the daily series, in the totals and in
  a one-day period (`An_order_paid_late_at_night_UTC_counts_on_the_next_morning_in_Hanoi`).
- **SC-002**: Both revenue responses start at Hanoi midnight: `from + 7 h = firstDay`, checked by Bruno against the
  rebuilt containers.
- **SC-003**: A default seller revenue period names exactly 30 days.
- **SC-004**: The chart draws `firstDay`..`lastDay` for dates far from today; the page test was red under the
  mutation that ignored them.
- **SC-005**: No regression: Order 264/264, Catalog 205/205, client 456/456, Bruno 267/267 requests and 435/435
  tests at the merge (from the pull request).

## Assumptions

- The shop's customers, sellers and staff live in one zone (Vietnam). A second zone is out of scope.
- Orders are dated by instants (`PaidAt ?? CreatedAt`, specs/072), so moving the day boundary needs no data change.
- PostgreSQL knows the IANA zone names (it ships the tz database).

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

The full reasoning, with rejected alternatives, is in [research.md](./research.md).

## Out of scope

- A time zone per seller, or per reader.
- Moving existing `product_views` rows.
