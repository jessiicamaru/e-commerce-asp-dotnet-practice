# Tasks: Honest view counts

- [X] T001 Gateway:
  - the `views` policy (30 a minute), and a `catalog-product-view-route` for `POST /api/products/{id}/view`;
  - a test that the third call over a limit of two is 429, another client still counts, and `GET` and `/saved` are
    never limited.
- [X] T002 Catalog:
  - `ProductViewer` and `product_viewers` (migration `AddProductViewers`, which only adds);
  - `RecordAsync(productId, day, viewer)` as one statement, with the claim, the count and the pruning of earlier
    days together;
  - the viewer hash taken from the token, or else from the body;
  - an optional body on the controller.
- [X] T003 Tests in `ProductViewTests`:
  - twenty visitors at once count twenty;
  - one visitor twenty times at once counts once;
  - a signed-in person counts once whatever visitor id they send;
  - naming nobody counts every time;
  - earlier days' viewers are dropped.
- [X] T004 Storefront: `visitorId()` in `utils/shared`, sent by `Product.recordView`. Tests cover:
  - the same id every time;
  - a stored id reused, and one that is not an id replaced;
  - none sent when storage is blocked.
- [X] T005 Mutations, Bruno, and the docs:
  - five mutations: no viewer, the body winning over the token, no pruning, no gateway policy, no reuse of the
    stored id;
  - Bruno: the same visitor opens the product twice, and "most viewed" sees one view;
  - docs: admin insights, security, the architecture's rate limits, CLAUDE.md, decisions, the reference, counts,
    timeline and backlog.
