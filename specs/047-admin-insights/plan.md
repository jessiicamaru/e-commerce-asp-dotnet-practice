# Implementation Plan: Admin insights

**Branch**: `047-admin-insights` | **Spec**: [spec.md](spec.md)

## Technical Context

- **Order**
  - `IOrderInsights` is implemented by `OrderInsights`, which groups in SQL over the orders that count as
    sold.
  - `InsightsHandlers` shapes the results per currency. An order from before specs/022 has no currency
    and counts as the default one.
  - Endpoints, all Admin only:
    - `GET /api/orders/insights/revenue`: totals and a daily series.
    - `GET /api/orders/insights/top-products`: `by=units` or `by=revenue`, with a `currency`.
    - `GET /api/orders/insights/top-buyers`: ids, with spend per currency.
  - A period runs from `from` (inclusive) to `to` (exclusive). The default is the last 30 days; the
    longest allowed is 366 days.
- **Catalog**
  - `product_views` has one row per product per day, keyed on (ProductId, Day).
  - `POST /api/products/{id}/view` is anonymous and always returns 204. It counts only a listed product
    viewed by a shopper.
  - `GET /api/products/insights/top-viewed` is Admin only.
- **Identity**
  - `GET /api/users/lookup?ids=` returns the email and name for each id.
  - `GET /api/users/stats` returns a count per role, plus how many accounts are locked or banned.
  - Both are Admin only.
- **Client**
  - `services/insights` and `hooks/insights`. Queries are keyed by the start of the period.
  - The product page reports a view once per product opened, using a ref guard.
  - `/admin/overview` is the first item in an administrator's sidebar. It shows headline cards, revenue
    cards per currency with a daily chart for the chosen currency, and three top-5 lists.

## Research

- **D1 - The client composes the page.** The Overview calls Order, Catalog and Identity separately. The
  alternative was a new service-to-service edge only for a report.
- **D2 - A view is its own request, not a side effect of `GET /products/{id}`.** The seller's page reads a
  product once per currency, and the storefront refetches when the tab regains focus. Counting reads
  would count both.
- **D3 - One increment per view, done by the database** (`ON CONFLICT DO UPDATE ... + 1`). Reading the
  count and writing it back would lose views that arrive at the same moment.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I | Each service answers from its own data; nothing new is shared between them. |
| III | Reads only, apart from the view counter, which is one idempotent-per-attempt statement. |
| IV | Every insight is Admin only, and a view counts only a shopper. |
| V | Order, Catalog and Identity tests, 2 mutation checks, Bruno, and client tests. |
