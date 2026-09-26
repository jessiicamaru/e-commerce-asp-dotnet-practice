# Tasks: Insights in the shop's days

- [X] T001 `InsightsCalendar` in `Ecommerce.Shared/Insights` (`DayOf`, `StartOf`, `ZoneId`) and
  `AddInsightsCalendar` reading `Insights:TimeZone`, refusing an unknown zone at startup; `InsightsPeriod.Resolve`
  and `ValidPeriod` take the calendar.
- [X] T002 Order: `OrderInsights` groups by the shop's date (`AT TIME ZONE`), the handlers and all five validators
  take the calendar, and `RevenueResponse` carries `FirstDay`/`LastDay`. Test: an order paid at 23:30 UTC counts on
  the next day in Hanoi; the existing insight tests use the shop's days.
- [X] T003 Catalog: a view is recorded on the shop's today; top viewed resolves its period with the calendar.
- [X] T004 Storefront: `periodDays(first, last)` from the response's `firstDay`/`lastDay`, falling back to the dates
  asked for; tests for the days drawn, with dates that are not today's.
- [X] T005 Mutations, Bruno against the rebuilt order, catalog and storefront, and the docs (admin and seller
  insights, CLAUDE.md, counts, timeline, backlog).
