# Tasks: Admin revenue less returns

- [X] T001 A test in `SellerInsightsTests`:
  - an order with two sellers' parcels, one returned and received with its refund;
  - another order whose return is still open;
  - admin revenue, top products and top buyers, checked against the seller's page.
- [X] T002 `OrderInsights`:
  - `ReturnedIn` gives the refund of each received return of a sold order;
  - revenue and buyers subtract it by their own keys;
  - products leave out the lines of a returned parcel.
- [X] T003 Mutations (each subtraction removed in turn), Bruno against the rebuilt Order, and the docs:
  admin and seller insights, returns, CLAUDE.md, decisions, counts, timeline and backlog.
