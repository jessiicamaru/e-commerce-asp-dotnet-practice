# Research: A product inserted without a review status waits for review

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #184

---

## D1 - Change the default; do not only document the risk

**Decision**: `ALTER TABLE products ALTER COLUMN "ReviewStatus" SET DEFAULT 'Pending'`.

**Rationale**: The default was chosen for the backfill in `20260923210256_AddProductReview`, which ran once. After it,
the only rows the default decides are those written by an image that does not know the column - by construction a
rollback. For such a writer the choice is between two mistakes:

| Default | A seller's product | The shop's own product |
| :-- | :-- | :-- |
| `Approved` | **on sale, unreviewed** | on sale (right) |
| `Pending` | waits for review (right) | waits for review |

The `Pending` mistake is visible in the moderators' queue, reversible by one approval, and costs shoppers nothing. The
`Approved` mistake defeats specs/045.

**Alternatives considered**:

- **Accept the risk and write it in the rollback guidance.** Rejected: a sentence nobody reads during an incident
  protects nothing, and the fix is one statement.
- **A trigger that sets `Pending` when `SellerId` is not null and `Approved` otherwise.** Rejected: more precise, but a
  trigger is logic outside the service code, invisible to the next reader, for a window that exists only during a
  rollback to a two-week-old image.

---

## D2 - SQL in the migration, not `HasDefaultValue` in the model

**Decision**: The migration uses `migrationBuilder.Sql(...)`; `ProductConfiguration` keeps no default and says why. The
model snapshot is unchanged.

**Rationale**: `ProductReviewStatus`'s first member, and so its CLR default, is `Approved`. When an EF model declares a
database default for a property, EF omits the property from an INSERT whenever its value equals the CLR default (the
"sentinel"), leaving the database to fill it. With `HasDefaultValue(Pending)` every administrator's product - written
`Approved` - would be omitted and stored `Pending`. Measured: with the model default added, the suite does not start
(EF refuses to migrate a model change no migration records), and the stored-status assertion exists for the case where
somebody also adds the migration.

**Alternatives considered**:

- **`HasDefaultValue` with `HasSentinel` set to a value never used.** Rejected: correct only while nobody touches it;
  the plain SQL has no trap.
- **Reorder the enum so `Pending` is the CLR default.** Rejected: the enum is stored as text, but its order is read by
  anybody who writes `default(ProductReviewStatus)`, and nothing else needs changing.

---

## D3 - No backfill, and `Down` restores the old default

**Decision**: No `UPDATE`; `Down` sets `'Approved'` again.

**Rationale**: Every existing row has an explicit status; the default never rewrote rows and does not now. Restoring the
old default on `Down` returns the schema to exactly what the previous image shipped with.

**Alternatives considered**: none - a backfill would have nothing to do.

---

## D4 - How it is tested

**Decision**: One test inserts a row by SQL naming only the columns an image from before specs/045 knew; the existing
shop-product test also reads the stored status. The Catalog test fixture creates a fresh database and migrates it, so
the migration itself is under test.

**Rationale**: Testing the migration through EF would test EF's INSERT, which always names the column - the path that
was never broken.
