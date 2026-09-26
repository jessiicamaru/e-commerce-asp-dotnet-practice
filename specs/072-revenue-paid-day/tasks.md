# Tasks: Revenue counts on the day an order was paid

- [ ] T001 Tests first. `InsightsTests` and `SellerInsightsTests`: placed on one day and paid on the next, for the admin and the seller; no `PaidAt`, so the day it was placed. `SettlementTests`: `PaidAt` is written once, and not for a failure.
- [ ] T002 `Order.PaidAt`, the migration, `SettleAsync`, `OrderInsights` (one `SaleDay`), and the detail response.
- [ ] T003 Mutation checks, the reference, and the docs (admin and seller insights pages, the timeline, the backlog, the counts).
