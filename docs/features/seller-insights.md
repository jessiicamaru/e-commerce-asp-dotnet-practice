# Seller insights

The **Insights** page, `/shop/insights`, shows a seller how their own shop is doing. It covers their revenue
per currency over the last 7, 30 or 90 days, with one bar per day, what sells most, how often their products
were viewed, and how they are rated. It is the administrator's [Overview](admin-insights.md) scoped to one
seller.

The page is built the same way as the Overview. The storefront composes it from two services, and each
answers from its own data:
- **Order** answers with money and what sold.
- **Catalog** answers with views and ratings.

Neither service is told who the seller is: the token says (Constitution IV).

## What people can do

| Role | Capabilities |
| :-- | :-- |
| Seller | Open **Insights** in the shop console and choose 7, 30 or 90 days. Read their revenue, orders and average per currency, and a daily chart for one chosen currency. Read their top 5 products by units, with the revenue of each per currency. Read their total product views and the 5 most viewed products, each with its rating. Read their overall rating and review count. |
| Customer, Administrator, Moderator | Nothing here. Each endpoint is `Seller` only, so they get 403. An administrator has the Overview. |

## Rules and guarantees

1. **Revenue is the seller's own lines**, unit price × quantity, before tax, less their own voucher (specs/069). It is never the order's total,
   which holds other sellers' goods, delivery and tax (specs/034). On an order two sellers share, each sees
   only their own lines.
2. **What counts as sold is the Overview's rule**, `OrderInsights.Sold`: paid, preparing or shipped. Never
   failed, cancelled or still settling.
3. **A parcel returned and refunded is not revenue.** A seller's part whose return reached `Received` is left
   out (specs/066). A return that is still open counts, because it may yet be refused. The return is one
   seller's parcel, so another seller's line on the same order still counts.
   - *Why (decision 51):* the seller's earnings already leave a returned part out. Two answers on one console
     would be read as a bug.
   - The admin Overview still counts it. That is a known limit, below.
4. **An order counts once** per day and currency, however many of the seller's lines it holds.
5. **Money is never added across currencies.**
6. **One period rule** (specs/055): whole UTC days, both ends included, at most 366.
7. **The rating is weighted by each product's review count.** One review at 4 and three at 2 give 2.5, not
   3.0. It comes from each product's stored `RatingAverage` and `RatingCount`, which are recomputed from the
   visible reviews on every write (specs/046). With no reviews at all it is null, never zero.
8. **Views** are the per-day counters of specs/047, summed over the period for the seller's own products. The
   seller's own visits were never counted.

## API

| Method | Path | Who | Answers |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/orders/sales/insights/revenue` | Seller | `RevenueResponse`, the admin's shape: `totals` per currency and `days` per currency and day. Takes `from` and `to`. |
| `GET` | `/api/orders/sales/insights/top-products` | Seller | `TopProduct[]` by units, with the revenue per currency. Takes `from`, `to` and `limit` (1-50, default 10). |
| `GET` | `/api/products/insights/mine` | Seller | `views`, `ratingAverage`, `ratingCount`, and `products` (the most viewed, each with `views`, `ratingAverage` and `ratingCount`). Takes `from`, `to` and `limit`. |

No message and no table is new. The reads use `order_items`, `order_shipments`, `parcel_returns`,
`products` and `product_views`.

## Storefront

| Path | What it does |
| :-- | :-- |
| `client/src/pages/shop-insights/` | The page: the period, two cards (views and rating), revenue, and two top-5 lists. |
| `client/src/components/insights/` | Shared with the Overview since specs/068: `DailyRevenueChart`, `RevenuePanel`, `RankedList`, `InsightPanel` and `PeriodPicker`. Their words are in `common` (`insights.*`). |
| `client/src/utils/insights/` | `periodRange`, which gives today and the N - 1 days before it: the chart's days. |
| `client/src/layouts/seller-layout/` | The **Insights** link, second in the shop console. |

## Tests

| Where | What it proves |
| :-- | :-- |
| `Ecommerce.Order.Tests/SellerInsightsTests` (11) | Revenue is the seller's lines before tax, never the order's total. Another seller's lines never appear. Only sold orders count. A received return is excluded and an open one is not. A return is one seller's part. An order counts once. Currencies are never added together. The period is whole days and has its limits. Top products are ranked by units, with revenue per currency. A token without an id is refused. |
| `Ecommerce.Catalog.Tests/SellerProductInsightsTests` (6) | Only the seller's own products, and views only inside the period, with the most viewed first. The rating is weighted. No reviews gives a null rating. The list is limited but the totals are not. The period rule. A token without an id is refused. |
| `client/src/pages/shop-insights/index.test.tsx` | What both services are asked for, over exactly the chart's days. Revenue per currency, never added together. The rating and each product's views and rating. Nothing yet rather than zeros. A new period asks again. |
| `client/src/services/insights/index.test.ts` | The three URLs, with no seller id. |
| `bruno/seller/` 56-59 | 200 for the seller with the right shape, including the product this folder listed. 403 for a customer on both services. |

## Known limits

- **The admin Overview still counts a returned sale.** Its revenue is order totals, and refunds do not reduce
  it (specs/066).
- **Days are UTC days.** Revenue is dated by when the order was paid (specs/072), as on the Overview.
- **No comparison with a previous period, and no export.**

## History

| Spec | PR | Added |
| :-- | :-- | :-- |
| [068-seller-insights](../../specs/068-seller-insights/) | #152 | The two Order endpoints, Catalog's `insights/mine`, and the Insights page, with the Overview's pieces moved to `components/insights` (#111). |
