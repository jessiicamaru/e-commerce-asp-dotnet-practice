# Implementation Plan: Review races and own-product reviews

> Completed on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Branch**: `057-review-races` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #127

## Summary

Make the three review writes that race into guarded single statements, each with its audit entry (and notice)
staged inside the statement's transaction only when the statement changed a row: a first review is `INSERT ... ON
CONFLICT DO NOTHING` (the loser edits), hide and restore are `UPDATE ... WHERE "HiddenAt" IS [NOT] NULL` (the loser
gets 409). A seller writing a review of their own product is refused with 403, and "mine" reports them not
eligible.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core with Npgsql (`ExecuteSqlInterpolatedAsync`, `ExecuteUpdateAsync`, the execution
strategy), MediatR, `Ecommerce.Shared.Audit`, `Ecommerce.Shared.Notifications`, MassTransit outbox

**Storage**: PostgreSQL, `ecommerce_catalog_db` (5433) - `product_reviews`, `products`; no schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, `ReviewTests`), concurrent requests with
`Task.WhenAll`, MassTransit's harness for published entries

**Target Platform**: Catalog service (5057)

**Constraints**: a decision and its record commit together or not at all; nothing staged for a write that did not
happen; the retrying execution strategy wraps any hand-opened transaction

**Scale/Scope**: three repository methods, four handlers

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

At the merge the three share one private `GuardedAsync(productId, stage, statement)` in `ReviewRepository`, and
`TryHideAsync` / `TryRestoreAsync` also take the product id for the recompute. The rating recompute
(`RecomputeAsync`) is the same statement `SaveAndRecomputeAsync` used, extracted.

> **Correction (2026-09-27, from the code at the merge):** T002 in [tasks.md](tasks.md) names a
> `DiscardPendingChanges` on `IReviewRepository`. That belonged to the first version described above; the merged
> interface has `TryAddFirstAsync`, `TryHideAsync` and `TryRestoreAsync` and no `DiscardPendingChanges`.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- III (atomic writes, idempotence): the guarded statement decides, and the audit entry commits with it
  or not at all. Pass.
- V (evidence): each race has a concurrent test that fails first. Mutation checks follow. Pass.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog decides from its own rows - the product's `SellerId` and its `review_eligibility` read model - with no call to Order or Identity |
| **II. Clean Architecture Layering** | **Pass.** The guarded SQL is in the Infrastructure repository behind `IReviewRepository`; the handlers pass a `stage` callback, so the Application layer decides *what* is recorded and the repository decides *when* |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and the point of the feature.** Each race is decided by one guarded statement; the audit entry and notice are staged and saved inside that statement's transaction only when it changed a row, so a lost race publishes nothing; the transaction runs inside `CreateExecutionStrategy().ExecuteAsync` |
| **IV. Identity Comes From the Token** | **Pass.** The reviewer and the moderator come from `ICurrentUser`; "own product" compares the token's id with the product's `SellerId` |
| **V. Evidence Over Assumption** | **Pass.** All three new tests failed before the fix for the issue's reasons (`23505 duplicate key ... IX_product_reviews_ProductId_CustomerId`; several of five concurrent hides succeeded; no refusal for the seller); the concurrency tests ran 5 times, 5/5 green; three mutations were each caught. The first design was abandoned because its test could not tell right from wrong - recorded above |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/057-review-races/
├── spec.md
├── plan.md            # This file
├── research.md        # D1-D4
├── data-model.md      # The three guarded statements
├── quickstart.md
├── contracts/
│   └── http-api.md    # Review endpoints: new answers
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IReviewRepository.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ReviewRepository.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs`
- `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs`
- `CLAUDE.md`, `docs/features/ratings-and-reviews.md`, `docs/overview/project-overview.md`,
  `docs/testing/testing-strategy.md`, `docs/project/backlog.md`, `docs/project/timeline.md`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- The edit path is not itself a guarded statement: two concurrent *edits* of an existing review each load, change
  and save it, and each records `ReviewEdited`. No test covers concurrent edits; the feature's concern was the
  first write, which is now guarded.
