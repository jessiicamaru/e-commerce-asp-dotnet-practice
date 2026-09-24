# Implementation Plan: Review races and own-product reviews

**Branch**: `057-review-races` | **Spec**: [spec.md](spec.md) | **Issue**: #127

## Design

- **Write.** Catch SQLSTATE 23505 from `SaveAndRecomputeAsync` on the insert path. Then:
  `_reviews.DiscardPendingChanges()` (`ChangeTracker.Clear`, as Payment does), and run the handler once
  more, which now finds the review and edits it.
  - *Why discard:* a failed `SaveChangesAsync` leaves its rows `Added`, and the next save would try
    them again (CLAUDE.md gotcha). That includes the staged outbox messages, which described a review
    the database never accepted.
- **Hide and restore.** `TryHideAsync(id, reason, by, now, stage)` and `TryRestoreAsync(id, stage)`
  each run in one transaction, through the execution strategy:
  - `ExecuteUpdate ... WHERE "HiddenAt" IS NULL` (or `IS NOT NULL` for restore);
  - if one row changed: stage the audit entry, `SaveChanges` for the outbox, recompute the rating;
  - commit, and return the rows changed. Zero rows is a `ConflictException`.
- **Own product.** In `WriteReviewCommand` and `GetMyReviewQuery`, a product whose `SellerId` is the
  caller is not eligible. The command throws `ForbiddenException` with its own sentence.

## Constitution check

- III (atomic writes, idempotence): the guarded statement decides, and the audit entry commits with it
  or not at all. Pass.
- V (evidence): each race has a concurrent test that fails first. Mutation checks follow. Pass.
