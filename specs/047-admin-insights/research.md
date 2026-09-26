# Research: Admin insights

> Written on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

D1-D3 were written with the plan (they were the first draft's whole Research section). D4-D14 record decisions
visible in the merged code, its comments and the two pull requests that the first draft did not write down.
Where the record gives no rejected alternative, this says "not recorded" rather than inventing one.

---

## D1 - The client composes the page

**Decision**: The Overview calls Order, Catalog and Identity separately, through the gateway. Each answers from
its own data; the storefront puts the answers side by side. Top buyers arrive as ids from Order, and the page
then asks Identity's lookup for their emails.

**Rationale**: No service needs another's data to answer its part. Order knows money, sales and buyers by id;
Catalog knows views; Identity knows people. Composing in the client adds no coupling between services, and the
cost - six reads and a seventh for emails - is paid by one administrator's page.

**Alternatives considered**:

- **A new service-to-service edge only for a report** (for example Order asking Identity for emails, or one
  service gathering the other two). Rejected in the first draft as "a new service-to-service edge only for a
  report"; the system's existing synchronous edges (Order → Catalog, Cart and Identity at checkout, Inventory →
  Catalog for stock ownership) each serve a checkout or a permission, not a page of figures.

---

## D2 - A view is its own request, not a side effect of `GET /products/{id}`

**Decision**: `POST /api/products/{id}/view` records a view. Reading a product never does. The product page sends
the view once per product id it opens, guarded by a `useRef`, fire and forget.

**Rationale**: The seller's page reads a product once per currency, and the storefront refetches when the tab
regains focus. Counting reads would count both. The guard makes choosing a variant, re-rendering or refetching
send nothing more; a failed view is swallowed because "a view that fails to count is not worth an error on the
page" (the component's comment).

**Alternatives considered**:

- **Count inside `GET /api/products/{id}`.** Rejected for the reason above: the number would measure how often
  the client reads, not how often a person looks.

---

## D3 - One increment per view, done by the database

**Decision**: `ProductViewRepository.RecordAsync` is one statement:
`INSERT INTO product_views ("ProductId", "Day", "Views") VALUES (@p, @d, 1) ON CONFLICT ("ProductId", "Day") DO UPDATE SET "Views" = product_views."Views" + 1`.

**Rationale**: Reading the count and writing it back would lose views that arrive at the same moment. The upsert
makes PostgreSQL serialise the increments on the row; the first view of the day inserts, every later one adds.
`ProductViewTests` fires twenty at once and expects twenty.

**Alternatives considered**:

- **Read the row, add one, save through EF.** Rejected: two concurrent requests read the same number and both
  write it plus one.

---

## D4 - A counter per product per day, not a row per view

**Decision**: `product_views` holds one row per (`ProductId`, `Day`) with a `Views` count.

**Rationale**: "The question is 'what do people look at', not 'who looked'" (`ProductView`'s comment). A day is
the finest grain the Overview asks for, and a counter keeps the table at products x days however popular a
product becomes.

**Alternatives considered**:

- **A row per view** (who, when). Rejected in the entity's comment for the reason above; it would also store a
  trail of which person opened which page, which nothing on the page needs.

---

## D5 - One definition of a sale, in one place

**Decision**: `OrderInsights.Sold` = `Paid`, `Completed`, `Preparing`, `Shipped`. Every Order insight reads
through `SoldIn(from, to)`, which applies it.

**Rationale**: Three reports with three definitions would disagree with each other on the same page. `Failed`
and `Cancelled` orders took no money (or gave it back); `Submitted` is still settling. `Completed` is included
because orders settled before specs/011 still carry it, and every read reports it as `Paid`. An order cancelled
after payment is `Cancelled` now, so it leaves the report retroactively - the report says what the shop kept.

**Alternatives considered**: not recorded.

---

## D6 - Money per currency, never added; a missing currency is the default one

**Decision**: every amount in every response is paired with its currency - `RevenueTotal.Currency`,
`List<CurrencyAmount>` on products and buyers. An order with `Currency = null` (from before specs/022) counts as
`CurrencyOptions.DefaultCurrency`. The storefront shows one card per currency and charts one at a time.

**Rationale**: The shop converts nothing (specs/022), so a sum of dong and dollars is neither. A null currency on
an old order is not a third currency; those orders were all priced in the default one.

**Alternatives considered**:

- **Convert to one reporting currency.** Rejected by specs/022's rule that the shop converts nothing; there is no
  exchange rate in the system to convert with.

---

## D7 - Rank money in one named currency; rank units across currencies

**Decision**: top buyers (and top products `by=revenue`) sort by the amount in `currency`, the default currency
when none is given; a row with nothing in that currency sorts as 0, ties broken by order count (buyers) or units
(products). Top products `by=units` sorts by units summed across currencies, ties by name.

**Rationale**: amounts in two currencies cannot be compared; units can - a unit is a unit.

**Alternatives considered**: not recorded.

---

## D8 - Revenue is the order's total; product revenue is goods before tax

**Decision**: revenue and a buyer's spend sum `orders.TotalAmount` (goods, tax and delivery, as charged). A top
product's revenue sums its lines' `Quantity * UnitPrice`.

**Rationale**: `TotalAmount` is what the saga charged, so it is what the shop took; a line has no share of
delivery and its tax is separate. The two measure different things and are not expected to reconcile, which is
why the Overview shows units for top products and does not display product revenue (docs/features/admin-insights.md).

**Alternatives considered**: not recorded.

---

## D9 - Who a view counts for, and a quiet answer either way

**Decision**: `RecordProductViewCommand` counts only when the product exists and `IsListed`, and the caller is not
an administrator, not a moderator, and not the product's seller. Otherwise it returns without writing. The
endpoint is anonymous and always answers 204.

**Rationale**: a seller checking their own listing and a moderator reviewing it are not interest from buyers.
`IsListed` is the same test the public listing and checkout use (specs/045), so a pending or rejected product
gathers no views. The same answer for counted and not counted means the endpoint "says nothing about the
product" (the handler's comment): a different status for an unlisted id would let anyone probe whether an id
exists and is waiting for review.

**Alternatives considered**: not recorded. Counting once per viewer was not attempted at the merge; every call
counts. specs/086 (#178) added it.

---

## D10 - Emails from Identity, by id

**Decision**: Order returns buyer ids only. Identity gains `GET /api/users/lookup?ids=` (1-100 ids, unknown ids
left out) returning `UserBrief(Id, Email, FirstName, LastName)`, Admin only.

**Rationale**: Identity owns people's details; Order records only the buyer's id (FR-003). Leaving unknown ids
out rather than failing lets the page show a short id for somebody whose account is gone.

**Alternatives considered**: not recorded, beyond D1's rejection of Order asking Identity itself.

---

## D11 - Administrators only; a moderator is refused

**Decision**: every insight read, the lookup and the stats carry `[Authorize(Roles = "Admin")]`, not
`StaffRoles.Staff`. The sidebar link is drawn for administrators only.

**Rationale**: "Staff is not enough: insights are an administrator's" (the Bruno request's docs). Revenue,
spending per person and emails of the biggest buyers are business figures, not moderation work.

**Alternatives considered**: not recorded.

---

## D12 - The period at the merge

**Decision**: Order resolves a period as `to` = now and `from` = `to` minus 30 days unless given, both as UTC
instants, and reads `CreatedAt >= from AND CreatedAt < to`. Every Order query refuses, when both are given, a
`from` that is not before `to`;
the revenue query also refuses more than 366 days (`InsightsPeriod.MaxDays`). Top-product and top-buyer `limit`
is 1-50, default 10. Catalog's `top-viewed` takes the dates of `from` and `to` and includes both days. A day in
the revenue series is the UTC date of `CreatedAt`.

**Rationale**: the day the order was placed is the only time an order recorded; "an order stamps no separate
payment time" (spec, Out of scope; the PR's "Worth knowing"). The 366-day cap bounds the daily series.

**Alternatives considered**:

- **The day the payment was taken.** Out of scope at the merge because no such column existed; specs/072 (#116)
  added `orders.PaidAt` and dates a sale by `PaidAt ?? CreatedAt`.

The two services' rules differ (instants against whole days, a cap on one query only). That was found later as
#125 and made one rule, whole days with both ends included, by specs/055.

---

## D13 - The chart draws every day of the period (#101)

**Decision**: `DailyRevenueChart` draws one column for each day that `periodDays(to, count)` lists - the UTC
`YYYY-MM-DD` days ending on the day `to` falls on - on a shared baseline. A day with no sales is empty. Bars
scale to the highest day, which is named above; the first and last days are labelled below; hovering or focusing
a column shows its date, amount and orders in a tooltip that stays inside the card. One currency at a time.

**Rationale**: the first chart drew a bar only for days that had revenue. With every seeded order placed on one
day, that one bar filled the whole chart, with no dates, no values and no baseline; "it read as nonsense, and it
was reported that way" (#101). A bar is never thinner than 2% of the height, so a small day beside a big one is
still visibly a sale.

**Alternatives considered**:

- **Draw only the days with revenue** - the #99 chart, rejected by the report above.
- **Put dong and dollars on one chart.** Rejected: they never share a scale (D6).

---

## D14 - How the tests stay honest on a shared database

**Decision**: `InsightsTests` moves each test's orders to a day of its own, far in the future (from 2031-01-01
plus a random number of days up to 3000), and asks about exactly that day. Bruno's "top" requests assert order
and shape - sorted, positive, one entry per currency - not that this run's product is in the list. The client
suite's `findBy`/`waitFor` timeout went from 1 to 3 seconds in #101.

**Rationale**: every other test in the collection places orders "now", so a period that included now would count
them. In Bruno, "a shared dev database has many one-unit products" (#99), so this run's product need not be in a
top 50. In the client, "with 51 test files running in parallel, the first render in a file sometimes took longer
than a second. Three unrelated tests failed that way consistently in the full run and passed on their own" (#101).

**Alternatives considered**: not recorded.
