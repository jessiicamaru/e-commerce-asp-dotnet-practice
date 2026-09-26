# Tasks: No reviews off the shelf

- [X] T001 A test in `UnlistedProductReadsTests`:
  - an eligible customer reviews a product on sale;
  - it is taken down, and a second review is a 404 while the page says not eligible;
  - the rating stays;
  - back on sale, a review is written again.
- [X] T002 `ReviewFeatures`: `WriteReviewCommand` and `GetMyReviewQuery` ask `OnSale` (`IsListed && IsActive`).
- [X] T003 Two mutations (one gate removed at a time), Bruno `seller/a review of it is a 404` (seq 82) against the
  rebuilt Catalog, and the docs: ratings and reviews, CLAUDE.md, counts, timeline and backlog.
