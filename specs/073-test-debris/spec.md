# Feature Specification: Test runs clean up after themselves

> Completed on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**Feature Branch**: `073-test-debris` | **Created**: 2026-09-26 | **Issue**: #118 (closes it)

**Status**: Merged (#157, 2026-09-26).

## Why

Every run of Bruno, `verify-saga.sh` and `verify-auth.sh` creates products and a category, and removes none. On
2026-09-24 there were 57 leftover products. That was enough to push a run's own product off the first page of a
search test and fail it. `local/purge-products.sh` and `seed/clean-test-debris.py` exist to clear this by hand.

## User Scenarios

### US1 - A test run leaves the catalogue as it found it (P1)

A developer or CI runs Bruno or one of the end-to-end scripts; afterwards the catalogue holds the same products and
categories it held before.

**Why this priority**: It is the whole change. Leftovers had already failed a search test and cluttered a catalogue
that real cameras were seeded into.

**Independent Test**: Count Catalog's products and categories, run the full Bruno collection twice and `verify-saga.sh`
once, count again.

**Acceptance Scenarios**:

1. **Given** a count of products and categories, **When** Bruno runs in full, **Then** the counts are the same
   afterwards, and each `teardown` delete answers 204 (or 404 when an earlier request already removed the thing).
2. **Given** a count, **When** `verify-saga.sh` or `verify-auth.sh` runs and passes, **Then** it prints what it deleted
   and the counts are unchanged.

### US2 - A failed run cleans up too, and still fails (P1)

**Why this priority**: A failed run is exactly when leftovers pile up, and a cleanup that swallowed the failure would
turn a red CI job green.

**Independent Test**: Force `verify-saga.sh` to fail (`SAGA_E2E_SCENARIO=reject` against an approving Payment); it
exits non-zero, still prints its deletes, and the counts are unchanged.

**Acceptance Scenarios**:

1. **Given** a script run that fails, **When** it exits, **Then** its product and category are deleted and its exit
   status is still the failure's.
2. **Given** a delete that fails (for example Catalog is down), **When** the cleanup runs, **Then** the failure is
   printed and does not change the exit status.

## Requirements

- **FR-001** Bruno ends with a `cleanup` folder, run last, that deletes as an administrator what the run
  created: the product, the seller's product, then the category (`DELETE`, which Catalog already has).
  - Each delete is asserted: 204, or 404 when an earlier request already removed it.
- **FR-002** `verify-saga.sh` and `verify-auth.sh` delete their product and category in a `trap ... EXIT`, so a
  failed run cleans up too.
  - The script's own exit status is kept.
  - A delete that fails is reported, never fatal.
- **FR-003** The purge tools stay, for what older runs left behind.
- **FR-004** Bruno's folder order is explicit: the seller folder runs after every other folder but the teardown, and
  the teardown runs last.

> **Correction (backfill)**: the folder that merged is named **`teardown`** (`bruno/teardown/`, `seq: 17`), not
> `cleanup`. FR-004 was added in the backfill from the PR's "Found while building".

Customers, orders and addresses are not deleted. They are the records of what happened, and nothing lists them
in a way that one run's leftovers can break.

## Acceptance

- Two consecutive full Bruno runs, and a `verify-saga.sh` run, leave Catalog's product count where it started.
- `verify-saga.sh` still passes, and exits non-zero when it fails.

### Edge Cases

- **The seller folder's order was accidental.** Its `folder.yml` used the old `meta:` format, so the Bruno CLI ignored
  its `seq: 9` and ran it after every sequenced folder. A teardown that did not know this ran before `seller`, deleted
  its category, and broke 19 requests. Both folders now carry an explicit `seq` (16 and 17).
- **Something earlier already deleted the product** (Bruno's own `DELETE /api/products/{id}` request): the teardown
  accepts 404.
- **`verify-saga.sh` fails before it creates its product**: the product delete is skipped (`PRODUCT_ID` unset); the
  category delete still runs.

### Key Entities

None new. The feature deletes Catalog products and categories through Catalog's existing endpoints.

## Success Criteria

- **SC-001**: Two consecutive full Bruno runs (232/232 requests, 379/379 tests each) leave Catalog at 48 products and 19
  categories before, between and after.
- **SC-002**: `verify-saga.sh` (`approve=pass`) and `verify-auth.sh` pass and leave the count unchanged, printing
  `cleanup: product … -> HTTP 204` and `category … -> HTTP 204`.
- **SC-003**: A forced failure exits with code 1, still deletes (204, 204), and the count stays at 48.

## Assumptions

- `DELETE /api/products/{id}` (Admin, specs/024) also removes Inventory's stock rows through `ProductDeletedEvent`, so
  deleting through Catalog cleans the other service too.
- A category can be deleted only once no product is filed under it - Catalog answers 409 otherwise - which is why the
  products go first.

## Out of scope

- Deleting customers, orders, addresses, sellers or vouchers a run created.
- Cleaning what older runs left behind - `seed/clean-test-debris.py` does that, keeping what `cameras.json` names.
