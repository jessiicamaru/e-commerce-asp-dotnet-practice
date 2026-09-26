# Feature Specification: A seller sees how their shop is doing

> Completed on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Feature Branch**: `068-seller-insights` | **Created**: 2026-09-25 | **Issue**: #111 (closes it)

**Status**: Merged (#152, 2026-09-26).

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

**Why this priority**: "How much did I sell" is the first question a seller has, and the one their console could not
answer at all.

**Independent Test**: With two sellers' lines on one paid order plus a failed order, ask
`GET /api/orders/sales/insights/revenue` as one seller: the total is exactly their lines before tax, the shared order
counts once, and the failed order not at all (`SellerInsightsTests`).

**Acceptance Scenarios**:

1. **Given** a paid order holding seller A's line of 2 × 100 and seller B's line of 1 × 50, plus tax and delivery,
   **When** A asks for revenue, **Then** A sees 200 and one order; B's line, the tax and the delivery appear nowhere.
2. **Given** A has three lines on one order, **When** A asks, **Then** it is one order, not three.
3. **Given** a failed, a cancelled and a still-settling order, **When** A asks, **Then** none counts.
4. **Given** A's parcel whose return reached `Received`, **When** A asks, **Then** its lines are not revenue; a return
   still open is.
5. **Given** a return of seller B's parcel on an order A shares, **When** A asks, **Then** A's lines still count.
6. **Given** sales in VND and USD, **When** A asks, **Then** there is one total per currency, never a sum.
7. **Given** a period longer than 366 days, or ending before it starts, **When** A asks, **Then** 400.
8. **Given** A's sales, **When** A asks for top products, **Then** A's products are ranked by units with revenue per
   currency.

---

### US2 - Views and ratings (P2)

The same page shows:
- how many times their products were viewed in the period;
- their most viewed products;
- each product's rating;
- the shop's overall rating, averaged over every visible review.

**Acceptance**: only the caller's own products are counted. The overall rating is weighted by each product's
review count, not a plain average of the product averages.

**Why this priority**: Views and ratings explain the revenue but do not replace it; P2 because the page is useful
without them and not without US1.

**Independent Test**: Give seller A two products - one with one review at 4, one with three reviews at 2 - and views
inside and outside the period; `GET /api/products/insights/mine` answers 2.5 over 4 reviews and counts only the views
inside the period (`SellerProductInsightsTests`).

**Acceptance Scenarios**:

1. **Given** A's and B's products with views, **When** A asks, **Then** only A's products' views inside the period
   count, most viewed first.
2. **Given** one review at 4 on one product and three at 2 on another, **When** A asks, **Then** the shop's rating is
   2.5 over 4 reviews, not 3.0.
3. **Given** a seller with no reviews, **When** they ask, **Then** the rating is null, never 0.
4. **Given** a limit of 5, **When** A has 8 products, **Then** 5 are listed but the totals cover all 8.

### Edge Cases

- **A caller whose token carries no user id** is refused rather than read as "the shop's own".
- **A seller with no sales** sees "Nothing yet." rather than a chart of zeros.
- **A product renamed since a sale** is listed by the newest name its lines carry.
- **A customer** asking any of the three endpoints gets 403.

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
- **FR-005** The seller's answers use the admin's response shapes (`RevenueResponse`, `TopProduct`), so one chart
  draws both consoles.

### Key Entities

- **Seller line**: one `order_items` row whose `SellerId` is the caller, on a sold order, not in a part whose return
  was received. The unit of seller revenue.
- **Seller product insights**: views, weighted rating and review count over the caller's products, plus the most
  viewed products.

## Success Criteria

- **SC-001**: A seller's revenue equals the sum of their own lines before tax on sold orders - never another seller's,
  never tax or delivery - checked by 11 Order tests and 9 of 9 mutations caught.
- **SC-002**: The shop's rating equals the review-count-weighted mean (one at 4 and three at 2 give 2.5).
- **SC-003**: A customer is refused (403) on all three endpoints - Bruno `seller/` requests 58 and 59.
- **SC-004**: Server suites green (Order 220/220, Catalog 167/167); storefront 378/378.

## Assumptions

- `products.RatingAverage` and `RatingCount` are right, being recomputed from the visible reviews in every review
  write, hide and restore (specs/046).
- Product views are counted per product per day by specs/047's upsert.
- A sale is dated by when it was placed - at this merge; see the note below.

> Later changes, recorded so the next reader is not misled: specs/069 (vouchers) made a seller's line revenue less
> the shop discount they paid for (`Quantity × UnitPrice - ShopDiscount`), and specs/072 dated a sale by
> `PaidAt ?? CreatedAt` instead of `CreatedAt`.

## Out of scope

- Comparison with a previous period, conversion rates, and exporting.
- Refunds on the admin Overview. It still counts a returned sale; that is a known limit of specs/066.
