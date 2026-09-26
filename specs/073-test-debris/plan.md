# Implementation Plan: Test runs clean up after themselves

> Written on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**Branch**: `073-test-debris` | **Spec**: [spec.md](spec.md) | **Issue**: #118 | **PR**: #157 (merged 2026-09-26)

No plan was written when the feature was built; this one is reconstructed from the merged change.

## Summary

Each test run deletes what it created in Catalog - its product(s) and its category - through Catalog's own `DELETE`
endpoints as an administrator: Bruno in a last folder, `teardown`, and the two shell scripts in a `trap ... EXIT` that
keeps the script's exit status. Bruno's folder order is made explicit on the way.

## Technical Context

**Language/Version**: Bash (the two scripts), Bruno OpenCollection YAML

**Primary Dependencies**: `curl`, the Bruno CLI (`@usebruno/cli`), Catalog's `DELETE /api/products/{id}` and
`DELETE /api/categories/{id}` (Admin)

**Storage**: none changed

**Testing**: the runs themselves, with Catalog's product and category counts read before and after; a forced failure

**Target Platform**: local runs and CI (`auth-smoke`, `saga-e2e`)

**Constraints**: a failed run must still fail; a failed delete must not fail the run; products before the category
(Catalog refuses a category that still has products)

**Scale/Scope**: three Bruno requests, two shell functions

## Design

- **Bruno** - `bruno/teardown/folder.yml` (`seq: 17`) with three requests as `adminToken`:
  1. `DELETE {{baseUrl}}/api/products/{{productId}}` - the product this run listed;
  2. `DELETE {{baseUrl}}/api/products/{{sellerProductId}}` - the seller's product (an administrator may delete
     anybody's);
  3. `DELETE {{baseUrl}}/api/categories/{{categoryId}}`.

  Each asserts `[204, 404]`: 404 when an earlier request already removed it.
- **Folder order** - `bruno/seller/folder.yml` moves from the old `meta:` format (which the CLI ignored, so `seller`
  ran last by accident) to `info:` with `seq: 16`; `teardown` is 17.
- **`verify-saga.sh`** - a `cleanup` function registered with `trap cleanup EXIT` right after the category and product
  exist: deletes the product (when `PRODUCT_ID` is set) and the category through the `status` helper (`curl ... ||
  true`), printing each HTTP code. The trap does not call `exit`, so the script's own status is kept.
- **`verify-auth.sh`** - the same, `auth_cleanup` with `trap auth_cleanup EXIT`.
- **Unchanged** - `local/purge-products.sh` and `seed/clean-test-debris.py` stay for older debris.

## Constitution Check

Against all five principles of [constitution.md](../../.specify/memory/constitution.md):

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The cleanup goes through Catalog's own API, never its database; Catalog's `ProductDeletedEvent` tells Inventory, which removes its own rows |
| **II. Clean Architecture Layering** | **Pass (not applicable).** No service code changed |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (not applicable).** No write path changed; a repeated delete is a 404 the teardown accepts |
| **IV. Identity Comes From the Token** | **Pass.** Deletes are made with a real administrator token through the gateway (Bruno) or Catalog (scripts); nothing is bypassed |
| **V. Evidence Over Assumption** | **Pass.** Counts read before, between and after two full Bruno runs and each script; a forced failure shown to exit 1 and still clean up. The first attempt was itself caught by evidence: a teardown that ran before `seller` broke 19 requests, which is how the accidental folder order was found |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/073-test-debris/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/README.md       # relies on Catalog's DELETE endpoints; changes none
└── checklists/requirements.md
```

### Source Code (touched at the merge)

```text
bruno/teardown/{folder.yml, delete the product this run listed.yml, delete the seller's product.yml, delete the category this run made.yml}
bruno/seller/folder.yml
.github/scripts/verify-saga.sh
.github/scripts/verify-auth.sh
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- Customers, orders, addresses, sellers and vouchers a run creates stay (by design).
- Debris from before this change still needs `seed/clean-test-debris.py`.
