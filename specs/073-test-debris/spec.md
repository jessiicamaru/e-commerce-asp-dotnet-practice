# Feature Specification: Test runs clean up after themselves

**Feature Branch**: `073-test-debris` | **Created**: 2026-09-26 | **Issue**: #118 (closes it)

## Why

Every run of Bruno, `verify-saga.sh` and `verify-auth.sh` creates products and a category, and removes none. On
2026-09-24 there were 57 leftover products. That was enough to push a run's own product off the first page of a
search test and fail it. `local/purge-products.sh` and `seed/clean-test-debris.py` exist to clear this by hand.

## Requirements

- **FR-001** Bruno ends with a `cleanup` folder, run last, that deletes as an administrator what the run
  created: the product, the seller's product, then the category (`DELETE`, which Catalog already has).
  - Each delete is asserted: 204, or 404 when an earlier request already removed it.
- **FR-002** `verify-saga.sh` and `verify-auth.sh` delete their product and category in a `trap ... EXIT`, so a
  failed run cleans up too.
  - The script's own exit status is kept.
  - A delete that fails is reported, never fatal.
- **FR-003** The purge tools stay, for what older runs left behind.

Customers, orders and addresses are not deleted. They are the records of what happened, and nothing lists them
in a way that one run's leftovers can break.

## Acceptance

- Two consecutive full Bruno runs, and a `verify-saga.sh` run, leave Catalog's product count where it started.
- `verify-saga.sh` still passes, and exits non-zero when it fails.
