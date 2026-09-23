# Tasks: Ratings and reviews

- [X] T001 Contract `ParcelDeliveredEvent`; Order announces each delivered parcel (confirm and sweep), the sweep locking what it sets
- [X] T002 Order tests: one event per parcel with its own products, never twice (confirm and sweep)
- [X] T003 Identity: `given_name` claim; `ICurrentUser.GivenName` with a default
- [X] T004 Catalog: eligibility and reviews tables, rating on products, migration; consumer; handlers; controller; gateway route
- [X] T005 Catalog tests (5): eligibility, one each + average, hidden not counted, seller told once, idempotent eligibility; 2 mutation checks
- [X] T006 Bruno: reviews folder, the refusal in the seller folder (it runs last), 401 without a token
- [X] T007 Client: stars, product reviews section, card average, staff page, wording; tests
- [X] T008 Run everything; verify-saga; docs
