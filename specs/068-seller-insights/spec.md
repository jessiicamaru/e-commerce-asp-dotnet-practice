# Feature Specification: A seller sees how their shop is doing

**Feature Branch**: `068-seller-insights` | **Created**: 2026-09-25 | **Issue**: #111 (closes it)

## Why

Administrators have the Overview (specs/047). A seller has lists of sales and payouts, and nothing that
answers the questions they actually have: how much they sold this month, what sells, what people look at,
and how their products are rated.

## User Scenarios

### US1 - Revenue and what sells (P1)

A seller opens **Insights** in their console and picks 7, 30 or 90 days. For each currency they see:
- their revenue;
- how many orders it came from;
- the average per order;
- one bar per day.

Below that is a list of their best-selling products, by units.

**Acceptance**:
1. Revenue is the sum of **their own lines** (unit price × quantity, before tax) on sold orders. It is never
   the order's total, which includes other sellers' goods, delivery and tax (specs/034).
2. What counts as sold is the admin Overview's rule: paid, preparing or shipped. Never failed, cancelled or
   still settling.
3. A parcel whose return was **received** is not revenue. Its money was refunded, and the seller's earnings
   already leave it out (specs/066).
4. Another seller's lines never appear, including lines on an order the two sellers share.
5. Money is never added across currencies.
6. The period is the one rule every insight uses (specs/055): whole UTC days, both ends included, at most 366
   days.

### US2 - Views and ratings (P2)

The same page shows:
- how many times their products were viewed in the period;
- their most viewed products;
- each product's rating;
- the shop's overall rating, averaged over every visible review.

**Acceptance**: only the caller's own products are counted. The overall rating is weighted by each product's
review count, not a plain average of the product averages.

## Requirements

- **FR-001** `GET /api/orders/sales/insights/revenue` and `GET /api/orders/sales/insights/top-products`
  (Seller). The seller is the token's subject; there is no seller id in the request (Constitution IV).
- **FR-002** `GET /api/products/insights/mine` (Seller): views in the period, the rating overall and per
  product, and the most viewed products.
- **FR-003** A seller-console page, `/shop/insights`, reusing the Overview's daily chart. It is in the menu.
- **FR-004** Tests on the server cover:
  - own lines only, and another seller's lines never counted;
  - sold only, and a received return excluded;
  - per currency;
  - the period rule;
  - views and ratings of their own products only, with the weighted average.

  Vitest tests cover what the page asks for and how it shows it.

## Out of scope

- Comparison with a previous period, conversion rates, and exporting.
- Refunds on the admin Overview. It still counts a returned sale; that is a known limit of specs/066.
