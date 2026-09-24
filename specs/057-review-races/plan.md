# Implementation Plan: Review races and own-product reviews

**Branch**: `057-review-races` | **Spec**: [spec.md](spec.md) | **Issue**: #127

## Design

- **Write.** `IReviewRepository.TryAddFirstAsync(review, stage)` runs one
  `INSERT ... ON CONFLICT ("ProductId", "CustomerId") DO NOTHING`. Only if it inserted does it stage the
  `ReviewPosted` entry and the seller's `NewReview` notice, save them for the outbox and recompute the
  rating, all in one transaction. A write that inserted nothing falls through to the edit path.
  - *Changed during implementation.* The first version caught the unique violation, discarded the failed
    write and retried as an edit. That is correct in production, where the outbox drops the refused
    attempt's messages with its transaction. But the test harness records a publish the moment it
    happens, so the test saw six `ReviewPosted` entries and could not tell right from wrong. The guarded
    insert never stages anything for an insert that did not happen. It is also the codebase's usual
    shape (a guarded statement plus `stage`), and it needs no exception-driven retry.
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
