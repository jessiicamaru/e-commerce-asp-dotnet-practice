# Feature Specification: Admin insights

> Completed on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature Branch**: `047-admin-insights` | **Created**: 2026-09-24 | **Issue**: #92

**Status**: Merged - #99 (2026-09-23T22:09Z, 2026-09-24 in local time, UTC+7, the date this record's headings
use) and the chart fix #101 (2026-09-24T04:28Z)

**Input**: issue #92, "an admin overview - revenue, top selling, top viewed, top buyers": revenue over a chosen
period per currency and per day, paid orders only, with order count and average order value; top selling
products by units and by revenue; top viewed products, which needs Catalog to start counting product page views;
top buyers by spend, with their email; headline numbers (customers, sellers, pending moderation work); an
Overview page in the admin console, all Admin-only.

## Why

The admin console had fulfilment and payouts, but gave an administrator no view of how the shop is
doing: revenue, what sells, what people look at, and who buys.

Three services each hold one part of that answer. Order knows what was paid, by whom and for what, but knows a
buyer only by id. Catalog knows products but, before this feature, recorded no view of any of them. Identity
knows who a person is and which roles they hold. None of them can answer the whole question alone, and the
constitution forbids any one of them reading another's database to try.

## User Scenarios & Testing *(mandatory)*

### US1 - Revenue (P1)

An administrator picks the last 7, 30 or 90 days. For each currency they see the revenue, the number of
orders and the average order value, plus a daily chart. Revenue counts paid orders only: never failed
orders, cancelled orders, or orders still settling. Dong and dollars are never added together.

**Why this priority**: revenue is the first question anybody running a shop asks, and it is the one where a
wrong definition does real damage - a report that counts cancelled or declined orders tells an administrator
the shop earned money it did not. Everything else on the page is secondary to getting this number right.

**Independent Test**: place orders in dong and in dollars, pay some, cancel one, let one fail; open the Overview.
There is one revenue card per currency, each equal to the sum of that currency's paid orders only, and the chart
shows one column for every day of the period in the selected currency.

**Acceptance Scenarios**:

1. **Given** a paid dong order, a shipped dong order and a dollar order being prepared in the period,
   **When** an administrator asks for revenue, **Then** there is one total for VND covering the two dong
   orders and one for USD covering the dollar order - never one number made of both.
2. **Given** a cancelled, a failed and a still-submitted order in the period, **When** revenue is asked for,
   **Then** none of them contributes to any total, count or day.
3. **Given** a currency's revenue of R over N orders, **When** revenue is shown, **Then** the average order
   value is R / N rounded to 2 places, halves away from zero.
4. **Given** a 30-day period in which every order was placed on one day, **When** the Overview draws the chart,
   **Then** it shows 30 columns on one baseline, 29 of them empty, the first and last day labelled, and the
   highest day named (#101).
5. **Given** revenue in dong and in dollars, **When** the administrator selects the dollar card, **Then** the
   chart shows only the dollar days, on a scale of its own.

---

### US2 - What sells, what is looked at, who buys (P1)

The page shows the top products by units sold, the most viewed products, and the buyers who spent most in
the chosen currency, each shown by email.

**Why this priority**: the three lists are what turn a revenue figure into something an administrator can act
on, and "most viewed" cannot be reported at all until Catalog starts counting - a count that starts late is a
count with a hole in it, so it shipped with the rest rather than after.

**Independent Test**: as a shopper, open a listed product's page; as an administrator, read the most viewed
list and find it counted once. Place orders for two products and read the top selling list in unit order;
read the top buyers and see emails, not ids.

**Acceptance Scenarios**:

1. **Given** paid orders for 3 units and then 1 unit of a lens, and 1 unit of a body, **When** top products are
   asked for by units, **Then** the lens comes first with 4 units and its revenue is listed per currency.
2. **Given** a cancelled order for 50 units of the body, **When** top products are asked for, **Then** those 50
   units count for nothing.
3. **Given** one buyer with two small dong orders (a 1,000₫ line each) and another with one large one (a
   50,000₫ line), **When** top buyers are asked for in VND, **Then** the large spender comes first and the
   other shows 2 orders.
4. **Given** top buyers as ids from Order, **When** the Overview shows them, **Then** each is named by the email
   Identity returns for that id.
5. **Given** a listed product, **When** 20 shoppers open its page at the same moment, **Then** its view count
   for the day rises by exactly 20.
6. **Given** a product, **When** its own seller, an administrator or a moderator opens it, or anyone opens a
   product that is pending review or does not exist, **Then** nothing is counted, and the answer is the same
   204 as for a counted view.
7. **Given** a product viewed 5 times and another viewed once, **When** the most viewed list is read, **Then**
   the first comes before the second with 5 views.

---

### US3 - Headline numbers (P2)

The page shows counts of customers, sellers and moderators, how many accounts are locked or banned, and
how much is waiting for moderation.

**Why this priority**: useful at a glance, but each number was already reachable elsewhere in the console
(the users page, the review queue, shop applications); the page gathers them rather than making them possible.

**Independent Test**: register a customer and approve a seller, then read the counts: customers up by 2 (a
seller holds `Customer` too), sellers up by 1.

**Acceptance Scenarios**:

1. **Given** the current accounts, **When** the stats are read, **Then** they give the total, a count per role
   (customers, sellers, moderators, administrators), the accounts locked until a future time and the banned
   accounts.
2. **Given** products waiting for review and shop applications waiting for a decision, **When** the Overview
   opens, **Then** it shows how many of each, and each card links to its queue.

---

### Acceptance (as first written)

1. Revenue equals the sum of paid, non-cancelled orders in the period, per currency.
2. Opening a product page raises its view count. The product's own seller and staff do not count, and
   neither does a product that is not on sale.
3. Only an administrator can read insights. A moderator and a customer get 403.

### Edge Cases

- **An order from before specs/022** has no currency. It counts as the default currency (`Money:DefaultCurrency`),
  never as a currency of its own called "null".
- **An order cancelled after it was paid** leaves the report: it is `Cancelled` now, so every period that
  contained it reads less revenue than it did before the cancellation.
- **A product renamed since it sold** appears in top selling under a name frozen on its order lines, not the
  catalogue's current one; Order does not ask Catalog. A deleted product still appears there.
- **A deleted product** drops out of the most viewed list: its `product_views` rows cascade away with it.
- **A product taken down after it was viewed** still appears in the most viewed list at the merge: the read
  joins `products` for the name and does not re-check that the product is on sale.
- **An anonymous shopper** counts as a view, the same as a signed-in customer.
- **A view of an id that does not exist**, or of a product pending review, is answered 204 like any other, so
  the endpoint cannot be used to learn whether an id is real.
- **A retried or repeated view request** counts again: nothing identifies a viewer at the merge (later
  limited per viewer and per client, specs/086).
- **A buyer with nothing in the ranking currency** sorts as zero there, ties broken by order count.
- **A period that starts after it ends** is refused with 400. **A revenue period longer than 366 days** is
  refused with 400; at the merge the other three insights had no such limit (see FR-011).
- **A lookup of no ids, or of more than 100**, is refused with 400; ids nobody holds are left out of the answer.
- **A moderator who types `/admin/overview`** sees the page frame, but every read on it answers 403; the
  sidebar link is drawn for administrators only.
- **Headline counts are for now, not for the period**, and "customers" includes sellers, who also hold
  `Customer`. "Stopped" on the page is locked plus banned, so an account that is both counts twice.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: One definition of a sale, used for revenue, top products and top buyers: Paid, Completed,
  Preparing or Shipped.
- **FR-002**: Views are counted per product per day, with an increment in the database that survives many
  simultaneous views.
- **FR-003**: Emails come from Identity by id. Order knows buyers only by id.
- **FR-004**: Every amount carries its currency. No total, list or chart adds one currency to another.
- **FR-005**: Revenue for a period MUST give, per currency, the revenue, the number of orders and the average
  order value, and one entry per currency per day with that day's revenue and orders.
- **FR-006**: Top products MUST be rankable by units sold (across currencies, since a unit is a unit) or by
  revenue in one named currency, each product carrying its units and its revenue per currency.
- **FR-007**: Top buyers MUST be ranked by spend in one named currency (the default currency when none is
  named), each carrying its order count and spend per currency.
- **FR-008**: A product view MUST be counted only when the product exists and is on sale and the viewer is
  neither staff nor that product's seller; the endpoint MUST answer the same way whether it counted or not.
- **FR-009**: A view MUST be its own request. Reading a product MUST NOT count as a view, and the product page
  MUST report one view per product opened, however often it renders.
- **FR-010**: Every insight, the user lookup and the user stats MUST be readable by an administrator only; a
  moderator is refused like a customer.
- **FR-011**: A period MUST default to the last 30 days and MUST start before it ends. The revenue query MUST
  refuse a period longer than 366 days. (The first draft of the plan stated the 366-day limit for every
  insight; the code at the merge applied it to revenue only - corrected here, and made uniform by specs/055.)
- **FR-012**: The people stats MUST give the total, a count per role, the accounts locked until a future time,
  and the banned accounts; the lookup MUST turn 1 to 100 ids into email and name, leaving out unknown ids.
- **FR-013**: The daily chart MUST show every day of the chosen period, including days with no sales, for one
  currency at a time (#101).

### Key Entities

- **Sale**: an order whose status is Paid, Completed, Preparing or Shipped. Carries its currency, its total as
  charged (goods, tax and delivery), its buyer's id, the day it was placed and its lines.
- **Product view count**: how many times a product's page was opened by shoppers on one day. One per product
  per day; not a record of who looked.
- **Revenue total / revenue day**: per currency, over the period or over one day - revenue, orders, and for the
  total the average order value.
- **Top product**: a product id, the name frozen on its most recent order line, units, and revenue per currency
  (goods before tax).
- **Top buyer**: a customer id, their order count and their spend per currency. An id until Identity names it.
- **User brief / user stats**: who an id is (email, first and last name); how many people hold each role and
  how many are stopped.

## Success Criteria *(mandatory)*

- **SC-001**: Mutation checks fail if cancelled orders count as revenue, or if staff views count.
- **SC-002**: In one Bruno run, every insight answers 200 to an admin, the view answers 204, a customer and a
  moderator get 403, and a request without a token gets 401. *Corrected on 2026-09-27*: the first draft said
  every insight answers 403 to a moderator and a customer and 401 without a token. The collection at the merge
  checks each refusal once - a customer on `revenue`, a moderator on `top-viewed`, no token on `revenue` - and
  relies on the shared `[Authorize(Roles = "Admin")]` for the rest.
- **SC-003**: Revenue equals the actual totals of the paid orders in the period, per currency, with cancelled,
  failed and submitted orders excluded - asserted against the stored `TotalAmount`, not a recomputation.
- **SC-004**: 20 simultaneous views of one product count as 20.
- **SC-005**: Every existing suite still passes: at #99 Order 179/179, Catalog 152/152, Identity 73/73, client
  225/225, Bruno 190 requests and 307 tests, and `verify-saga.sh`; at #101 the client 232/232, three full runs
  in a row.
- **SC-006**: The Overview has no horizontal overflow at 390px (screenshots at 1360px and 390px in #99, 1500px
  and 390px in #101).

## Assumptions

- The Overview is read by an administrator, a page at a time. No load or latency target was set; none was
  measured (not recorded).
- Revenue is the order's `TotalAmount` as charged, delivery and tax included. A top product's revenue is its
  lines' `Quantity * UnitPrice`, goods before tax. The two measure different things and are not expected to
  reconcile.
- Days are UTC days at the merge (later the shop's own time zone, specs/082).
- Catalog records views from this feature on; there is nothing to backfill, so "most viewed" starts empty.
- The waiting counts reuse the existing moderation reads (the review queue from specs/045 and shop applications
  from specs/044), which staff can already call.

## Out of scope

- Charts beyond a daily bar per currency, exports, and custom date pickers.
- Revenue by the time payment was taken. The day used is the day the order was placed; an order stamps no
  separate payment time. (Added later by specs/072, `orders.PaidAt`, #116.)
- A seller's view of their own shop (specs/068), revenue less returned parcels (specs/084), and counting a view
  once per viewer (specs/086) - each came later.
