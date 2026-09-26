# Research: Review races and own-product reviews

> Written on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

The pull request records D1 and D4 as decided on the user's behalf.

---

## D1 - A lost first-review race edits; it is not a 409

**Decision**: When `TryAddFirstAsync` inserts nothing, the handler loads the review that won and edits it with the
losing request's rating and text, as any second write does.

**Rationale**: The customer meant to review once. The second write of any review is already an edit, so a race is
resolved the way a slow second click would be.

**Alternatives considered**:

- **Answer 409.** Rejected: the customer did nothing wrong, and a slow second click is not refused either.

---

## D2 - `INSERT ... ON CONFLICT DO NOTHING`, not catching the unique violation

**Decision**: `TryAddFirstAsync` issues one `INSERT INTO product_reviews (...) VALUES (...) ON CONFLICT ("ProductId",
"CustomerId") DO NOTHING`; the `stage` callback (the `ReviewPosted` entry and the seller's `NewReview` notice), the
save and the rating recompute run only if it inserted one row.

**Rationale**: Nothing is staged for an insert that did not happen, it needs no exception-driven retry, and it is
the codebase's usual shape - a guarded statement plus `stage` (as parcel moves, payouts and cancellation do).

**Alternatives considered**:

- **Catch the unique violation, discard the failed write, retry as an edit.** Built first and replaced. It is
  correct in production - the outbox drops the refused attempt's messages with its transaction - but MassTransit's
  test harness records a publish the moment it happens, so the test saw six `ReviewPosted` entries and could not
  tell right from wrong.

---

## D3 - Hide and restore are guarded `UPDATE`s with the entry staged inside

**Decision**: `UPDATE product_reviews SET "HiddenAt" = @now, "HiddenReason" = @reason, "HiddenBy" = @by WHERE "Id" =
@id AND "HiddenAt" IS NULL` (restore: the reverse, `IS NOT NULL`), through `ExecuteUpdateAsync`, in a transaction
opened inside the execution strategy; one row changed → stage `ReviewHidden` / `ReviewRestored`, save, recompute,
commit; zero rows → the handler throws `ConflictException`.

**Rationale**: Every other moderation decision in this codebase is a guarded single-statement `UPDATE`; reading
`HiddenAt` then saving let two moderators both succeed and both be recorded.

**Alternatives considered**:

- **Keep read-then-save.** Rejected: it is the race. No other alternative is recorded.

---

## D4 - Nobody reviews what they sell

**Decision**: `WriteReviewCommand` throws `ForbiddenException("You cannot review your own product.")` when the
product's `SellerId` is the caller, before the eligibility check; `GetMyReviewQuery` reports `eligible: false` in the
same case.

**Rationale**: A review is the one signal a shopper reads as independent. It applies to the product's owner only: a
seller can still review somebody else's product they received. It is a 403 with its own sentence, like the existing
"not eligible" refusal - which fits the codebase's rule that a 403 is for a known caller who is told no (specs/043);
the pull request gives no further reason for the status.

**Alternatives considered**: none recorded.
