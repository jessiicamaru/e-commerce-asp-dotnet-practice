# Tasks: A seller sees how their shop is doing

- [X] T001 [US1] Tests first in `server/tests/Ecommerce.Order.Tests/SellerInsightsTests.cs`. Cover:
  - own lines only;
  - a shared order;
  - sold statuses only;
  - a received return excluded;
  - orders counted once;
  - per currency;
  - the period rule;
  - top products.
- [X] T002 [US1] Order: the queries, the repository rows, the grouping shared with the admin's insights, and `SalesInsightsController`.
- [X] T003 [US2] Tests in `server/tests/Ecommerce.Catalog.Tests/SellerProductInsightsTests.cs`, then the query, the repository method and the route.
- [X] T004 [US1] [US2] Storefront:
  - the service calls, the hook, and the chart moved to `components/insights`;
  - `pages/shop-insights`, with its route, its menu entry and its words in vi and en;
  - Vitest tests.
- [X] T005 Bruno (the seller folder: 200 for the seller, and a customer 403) and the reference regenerated. Then docs: the feature page, the timeline, the backlog and the counts.
