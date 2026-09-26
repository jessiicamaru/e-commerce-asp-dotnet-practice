# Feature Specification: A product inserted without a review status waits for review

**Feature Branch**: `093-review-default-pending` | **Created**: 2026-09-27 | **Issue**: #184

**Status**: Draft

**Input**: Issue #184 - "during a rollback a seller's new product goes on sale unreviewed".

## Why

Specs/045 added `products.ReviewStatus` with a **database default of `'Approved'`**, so that every product from before
review existed stayed on sale. That backfill happened once, when the column was added. Since then the default has
only one reader: a writer that does not know the column.

Such a writer exists by design. Rolling back to an earlier image is supported (specs/006), and the expand/contract
rule keeps the schema readable by an earlier image. An image from before specs/045 inserts a seller's new product
**without** `ReviewStatus`, and the default files it as `Approved` - **on sale, unreviewed**. The schema rule protected
the columns; nothing protected the meaning of the default.

## User Scenarios & Testing *(mandatory)*

### US1 - A product whose writer says nothing about review waits for review (Priority: P1)

A row inserted into `products` without `ReviewStatus` is `Pending`: not in the listing, not at its public address, not
sellable, and in the moderators' queue.

**Why this priority**: It is the defect - the one path by which a seller's product reaches the shelf without a
moderator.

**Independent Test**: Insert a product row by SQL without the column (what a pre-045 image does); it is stored
`Pending`, a shopper's lookup is null and search does not find it.

**Acceptance Scenarios**:

1. **Given** an insert that omits `ReviewStatus`, **When** it commits, **Then** the row's status is `Pending`.
2. **Given** that row, **When** a shopper opens or searches for it, **Then** it is not there.
3. **Given** that row, **When** a moderator opens the queue, **Then** it is waiting.

---

### US2 - Everything the current code writes is unchanged (Priority: P1)

The current image always writes the column: a seller's product `Pending`, the shop's own (an administrator listing it)
`Approved`. Both are stored exactly as written.

**Why this priority**: The obvious way to express the new default - `HasDefaultValue(Pending)` in the EF model - would
make EF leave `Approved`, the enum's CLR default, **out** of every INSERT, and the database would file the shop's own
products as `Pending`. The fix must not trade one wrong default for another.

**Independent Test**: An administrator creates a product; the **stored** status (read from the database, not from the
response) is `Approved`.

**Acceptance Scenarios**:

1. **Given** an administrator lists a product, **Then** it is stored `Approved` and on sale.
2. **Given** a seller lists a product, **Then** it is stored `Pending` (unchanged, specs/045).

### Edge Cases

- **The shop's own product inserted by a pre-045 image.** Also `Pending`: that image cannot tell a seller from an
  administrator in this column, so both wait. A moderator approves it; it is the safer of the two mistakes (Decision).
- **Existing rows.** Untouched: only the default changes, no row is rewritten.
- **Rolling back this migration.** `Down` restores `'Approved'`; an image from before this change never relied on the
  default being either value, since it writes the column (specs/045 onward) or predates review.
- **An image between specs/045 and this one.** Writes the column explicitly, like the current code; unaffected.
- **A hand-written insert (seed, data fix).** Must now name `ReviewStatus` to be on sale - the intended effect.
  `seed/seed-catalogue.py` goes through the API, which writes it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The database default of `products.ReviewStatus` is `'Pending'`.
- **FR-002**: The change is made by SQL in a migration; the EF model declares **no** default for the column, and a
  comment on the mapping says why.
- **FR-003**: `Down` restores `'Approved'`.
- **FR-004**: No row is rewritten, and no column is added, dropped, renamed or narrowed.
- **FR-005**: A test holds the omitted-column insert to `Pending`, and another holds the administrator's insert to a
  **stored** `Approved`.

### Key Entities

- **Product** - `ReviewStatus` (`Approved` / `Pending` / `Rejected`, text, required).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `A_product_inserted_without_a_review_status_waits_for_review` fails before the migration and passes after.
- **SC-002**: `The_shops_own_product_is_on_sale_as_listed` asserts the stored status and passes.
- **SC-003**: The migration's default reverted to `'Approved'` turns SC-001's test red; a model-level default fails the
  suite.
- **SC-004**: `Ecommerce.Catalog.Tests` passes in full.

## Decision

**Change the default to `Pending`, rather than accept the risk and document it.** The issue offered both. The change is
one statement, touches no row and no column shape, and turns the failure mode from "a seller's product on sale without
review" into "the shop's own product waits for a moderator during a rollback" - the second is visible, reversible and
harmless to shoppers; the first is the thing specs/045 exists to prevent. Recorded as decided on the user's behalf
([research.md](research.md) D1).

## Assumptions

- Images from before specs/045 are still reachable by a rollback within the kept `sha-` versions (30 per image).
- The column stays required; only its default changes.

## Out of scope

- A general audit of other columns' defaults against earlier images.
