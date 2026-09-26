# Tasks: A shopper saves a product for later

- [ ] T001 Tests first, in `SavedProductTests`:
  - save is idempotent under concurrency;
  - unsaving something unsaved is fine;
  - a hidden product cannot be saved (404);
  - the list is newest first and holds only the caller's;
  - a product taken down reads as unavailable;
  - a deleted product leaves the list;
  - the ids.
- [ ] T002 The entity, configuration, migration, repository, features and controller.
- [ ] T003 Back in stock: the rollup reports the flip, the handler notifies the savers, and the kind is declared. Tests: one notice per flip, none for an unlisted product, none on a repeat.
- [ ] T004 Storefront: the service, hooks, heart, page, route, menu and words. Vitest.
- [ ] T005 Bruno, the reference, mutation checks, and the docs.
