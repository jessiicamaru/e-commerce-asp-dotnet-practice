# Implementation Plan: A seller sees how their shop is doing

**Branch**: `068-seller-insights` | **Spec**: [spec.md](spec.md) | **Issue**: #111

## Design

**Order** (`Application/Insights/InsightsFeatures.cs`, `Infrastructure/.../OrderInsights.cs`)
- `GetSellerRevenueQuery` and `GetSellerTopProductsQuery` return the admin's response shapes
  (`RevenueResponse`, `TopProduct`), so the storefront draws both with one chart.
- The grouping in the handler is shared with the admin's queries: rows in, totals and days out.
- **The rows**, `IOrderInsights.SellerRevenueByDayAsync` and `SellerProductSalesAsync`, are built in SQL:
  - they read `order_items` where `SellerId` is the caller, on an order in `Sold` inside the period;
  - they skip a line whose part (`order_shipments` of `(OrderId, SellerId)`) has a return in `Received`;
  - revenue is `Quantity × UnitPrice`;
  - orders are counted **distinct per day and currency**, because one order can hold several of a seller's
    lines.
- A new `SalesInsightsController`, at `api/orders/sales/insights` with `[Authorize(Roles = "Seller")]`.

**Catalog** (`Application/Products/Views/ProductViewFeatures.cs`)
- `GetMyProductInsightsQuery(From, To, Limit)` returns `SellerProductInsights(Views, RatingAverage,
  RatingCount, Products[ProductId, Name, Views, RatingAverage, RatingCount])`, with the most viewed products
  first.
- The rating is read from `products.RatingAverage` and `RatingCount`. Those columns are already recomputed
  from the visible reviews on every write (specs/046), so the overall figure is
  `Σ(avg × count) / Σ count`. It is null when there are no reviews.
- `IProductViewRepository.SellerAsync(sellerId, from, to, limit)` does the grouping in SQL.
- The route is `GET api/products/insights/mine` with `[Authorize(Roles = "Seller")]`.

**Storefront**
- `Insights.sellerRevenue`, `sellerTopProducts` and `mine`, with the hook `useSellerInsights`.
- The chart moves from `pages/admin-overview/daily-chart.tsx` to `components/insights/daily-revenue-chart`,
  and its words move to `common` (`chart.*`), because it now serves two consoles.
- The new page `pages/shop-insights` is routed at `/shop/insights` and added to the menu.

## Research

- **D1 - a received return is not revenue here.** The admin Overview still counts it: that is a known limit of
  specs/066, and fixing it belongs in its own change. For the seller, the number sits beside earnings that
  already exclude it, and two answers on one console would be read as a bug.
- **D2 - the same response shapes as the admin's.** One chart and one set of types, rather than a parallel
  "seller" family that drifts.
- **D3 - the rating comes from the product's stored average, not from counting reviews again.** It is kept
  right in the transaction of every review write, hide and restore (specs/046). Counting again would be a
  second definition.
- **D4 - the Catalog endpoint takes no seller id.** The caller's id is the owner (Constitution IV), the same
  as `/api/orders/sales`.

## Constitution check

- **I (service autonomy):** each service answers from its own data, and the page composes the two answers.
- **IV (identity from the token):** there is no seller id in any request.
- **V (evidence):** tests against the real PostgreSQL, mutation checks, and Bruno checks for 200 as a seller
  and 403 as a customer.
