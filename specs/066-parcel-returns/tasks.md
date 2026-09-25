# Tasks: Returning a delivered parcel (part 1 - the server)

- [ ] T001 [US1] [US2] [US3] Tests first in `Ecommerce.Order.Tests/ReturnTests.cs`. Cover:
  - request rules: owner only, delivered only, inside the window, once, and once when two requests race;
  - accept and refuse by the right party only;
  - escalate, and an admin's final word;
  - sent back within the window;
  - received publishes the event with the right amount;
  - every late or second move is 409;
  - the notices.
- [ ] T002 [US4] Tests in `Ecommerce.Order.Tests/ReturnMoneyTests.cs`. Cover:
  - due only after the window;
  - an open return holds the money;
  - a returned part is never money;
  - the payout claim agrees with the balance.
- [ ] T003 Order: the entity, configuration, migration, repository, commands, routes, read models and notices.
- [ ] T004 Payment: `refunds.ReturnId` and its indexes, the consumer and the command, with tests.
- [ ] T005 Inventory: `returned_parcels`, the consumer and the command, the announcement, with tests (including `AnnouncementTests`).
- [ ] T006 Storefront words for the five notices. Bruno: the round trip on the shop's parcel, and the negative cases.
- [ ] T007 End to end, mutation checks, docs.
