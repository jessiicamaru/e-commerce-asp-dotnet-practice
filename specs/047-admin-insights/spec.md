# Feature Specification: Admin insights

**Feature Branch**: `047-admin-insights` | **Created**: 2026-09-24 | **Issue**: #92

## Why

The admin console had fulfilment and payouts, but gave an administrator no view of how the shop is
doing: revenue, what sells, what people look at, and who buys.

## User Scenarios

### US1 - Revenue (P1)

An administrator picks the last 7, 30 or 90 days. For each currency they see the revenue, the number of
orders and the average order value, plus a daily chart. Revenue counts paid orders only: never failed
orders, cancelled orders, or orders still settling. Dong and dollars are never added together.

### US2 - What sells, what is looked at, who buys (P1)

The page shows the top products by units sold, the most viewed products, and the buyers who spent most in
the chosen currency, each shown by email.

### US3 - Headline numbers (P2)

The page shows counts of customers, sellers and moderators, how many accounts are locked or banned, and
how much is waiting for moderation.

**Acceptance**
1. Revenue equals the sum of paid, non-cancelled orders in the period, per currency.
2. Opening a product page raises its view count. The product's own seller and staff do not count, and
   neither does a product that is not on sale.
3. Only an administrator can read insights. A moderator and a customer get 403.

## Requirements

- **FR-001**: One definition of a sale, used for revenue, top products and top buyers: Paid, Completed,
  Preparing or Shipped.
- **FR-002**: Views are counted per product per day, with an increment in the database that survives many
  simultaneous views.
- **FR-003**: Emails come from Identity by id. Order knows buyers only by id.

## Out of scope

- Charts beyond a daily bar per currency, exports, and custom date pickers.
- Revenue by the time payment was taken. The day used is the day the order was placed; an order stamps no
  separate payment time.

## Success Criteria

- **SC-001**: Mutation checks fail if cancelled orders count as revenue, or if staff views count.
- **SC-002**: In one Bruno run, every insight answers 200 to an admin, 403 to a moderator and a customer,
  and 401 without a token.
