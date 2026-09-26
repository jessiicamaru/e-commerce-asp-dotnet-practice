# Research: Insights in the shop's days

> Written on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-26

Six decisions. D1, D3, D4 and D5 are the spec's Decisions, expanded; D1 is also
[decisions.md row 64](../../docs/project/decisions.md). D2 and D6 are read from the code and the pull request.

---

## D1 - The shop's zone, not the reader's and not UTC

**Decision**: one zone for the whole shop, `Insights:TimeZone` (IANA), default `Asia/Ho_Chi_Minh`.

**Rationale**: two administrators in two countries must see the same numbers, and a seller's day is the shop's
day. UTC was wrong for the people the shop serves: seven hours of every local day counted on the day before.

**Alternatives considered**:

- **The reader's zone**, from the browser. Rejected: every total would depend on who asks, and a period cached for
  one reader would be wrong for the next.
- **Keep UTC and label the chart "UTC".** Not recorded as considered; it would leave the issue's order on the wrong
  day.
- **A zone per seller.** Out of scope.

---

## D2 - Resolved once, at startup; an unknown zone stops the service

**Decision**: `AddInsightsCalendar` calls `TimeZoneInfo.FindSystemTimeZoneById` at registration and turns
`TimeZoneNotFoundException` / `InvalidTimeZoneException` into an `InvalidOperationException` naming the id and the
default. The calendar is a singleton. `ZoneId` hands PostgreSQL the IANA id, converting a Windows id with
`TryConvertWindowsIdToIanaId` when the machine gave one.

**Rationale**: the constitution's Configuration rule - a service that cannot use its configuration fails at
startup rather than at every request. Falling back to UTC silently would reproduce the issue with no sign.

**Alternatives considered**:

- **Resolve per request.** Rejected: the same failure would surface as a 500 on the first insight, long after
  deploy.

---

## D3 - Grouped in PostgreSQL with `AT TIME ZONE`

**Decision**: `OrderInsights` groups by
`TimeZoneInfo.ConvertTimeBySystemTimeZoneId(o.PaidAt ?? o.CreatedAt, timeZone).Date`, which Npgsql translates to
`AT TIME ZONE`, and the seller's lines are dated the same way. The period filter compares `timestamptz` columns
with `InsightsPeriod.Start` / `End`, which are now the shop's midnights in UTC.

**Rationale**: the grouping must happen where the rows are, or every order of the period has to come back to the
service to be bucketed. `EF.Functions.AtTimeZone` has no `DateTime` overload, so the `TimeZoneInfo` call is the
form Npgsql understands.

**Alternatives considered**:

- **`EF.Functions.AtTimeZone`.** Rejected: no `DateTime` overload.
- **Group in memory.** Not recorded as considered; rejected here for the reason above.

---

## D4 - No migration

**Decision**: nothing in either database changes. Orders are dated by instants and regroup by themselves.
`product_views` holds a `date` per product; rows written before the merge keep their UTC day.

**Rationale**: the stored views have no hour, so moving them to Hanoi days would mean inventing one. The error is
bounded to the views of the days before the merge.

**Alternatives considered**:

- **Shift every stored view by a day, or split it.** Rejected: there is no information to split it by.

---

## D5 - The response names its days; the chart draws them

**Decision**: `RevenueResponse` gains `FirstDay` and `LastDay` (`DateOnly`, serialised `YYYY-MM-DD`).
`periodDays(first, last)` in `client/src/utils/insights` lists every day between them, both included, and both
pages call it with `firstDay ?? from` and `lastDay ?? to`. Unreadable or reversed ends give an empty list.

**Rationale**: the browser's clock knows neither the shop's zone nor what the server counted. Having the server say
which days it counted removes the second calendar. The fields are additions, so old and new pieces work together in
either order.

**Alternatives considered**:

- **Compute the Hanoi days in the browser.** Rejected: it would be a second copy of the zone, in another language,
  and a configuration change on the server would silently leave the chart behind.
- **The previous signature `periodDays(to, count)`.** Replaced: it counted back from the browser's UTC date.

---

## D6 - The validator resolves the period in the shop's days too

**Decision**: `ValidPeriod(from, to, calendar)` validates the period `Resolve` would produce, with the same
calendar.

**Rationale**: two instants on one Hanoi day can straddle a UTC midnight; counted in UTC days, `from` would fall
after `to` and a valid request would be refused (comment in `InsightsPeriod.cs`).

**Alternatives considered**: none recorded.

---

## A flaky test that followed

The first CI run of [specs/083](../083-more-emails/)'s pull request (#171) was red on
`InsightsTests.A_single_day_is_a_period`: tests that drew the same random day counted each other's orders. It was
fixed there with `InsightDays`, which hands out days ten apart. Recorded here because the Hanoi-day `Day()` helper
this feature introduced is what that fix replaced.
