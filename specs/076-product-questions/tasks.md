# Tasks: A shopper asks a seller about a product

- [ ] T001 Tests first, in `ProductQuestionTests`:
  - asking needs a product on sale, and a seller does not ask about their own;
  - the list is public, newest first, without hidden questions and with a hidden answer blanked;
  - only the seller answers a seller's product, and an administrator on it is a 404;
  - staff answer the shop's own;
  - the first answer tells the asker and a rewrite does not;
  - concurrent first answers produce one notice;
  - hiding and restoring, with who is told;
  - a hidden answer cannot be rewritten;
  - the queues.
- [ ] T002 The entity, configuration, migration, repository, features, controller and gateway route.
- [ ] T003 The notification kinds: the declaration and the `Notifier` constants.
- [ ] T004 Storefront: the service, hooks, product-page section, the seller and admin queues, routes, menu links
  and words. Vitest.
- [ ] T005 Bruno, the reference, mutation checks, and the docs.
