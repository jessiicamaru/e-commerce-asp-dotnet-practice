# Research: Test runs clean up after themselves

> Written on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-26 (decision 54 in
[docs/project/decisions.md](../../docs/project/decisions.md))

No research file was written at the time; these decisions are taken from the spec, the PR and the code. Who took them
is not recorded.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - Each run removes what it made, through the API

**Decision**: Bruno and both scripts delete their own product(s) and category through Catalog's `DELETE` endpoints as
an administrator, products first.

**Rationale**: The debris was a failure mode, not untidiness: 57 leftover products pushed a run's own product off the
first page of `search without diacritics`. Going through the API means every row goes the way a person's would -
Catalog announces `ProductDeletedEvent`, and Inventory drops its stock rows too. Catalog refuses a category that still
has products, so products go first.

**Alternatives considered**:

- **Keep clearing by hand** with `local/purge-products.sh` / `seed/clean-test-debris.py`. Rejected as the only answer:
  it is what had been done, and leftovers still reached 57. The tools stay for older debris.
- *(reconstructed)* **Delete with SQL.** Rejected: it would skip Inventory's stock rows and the audit trail.

---

## D2 - The scripts clean up in a `trap ... EXIT`

**Decision**: `trap cleanup EXIT` (`verify-saga.sh`) and `trap auth_cleanup EXIT` (`verify-auth.sh`); each delete
prints its HTTP code; the trap never calls `exit`.

**Rationale**: A failed run is exactly when debris piles up, so cleanup must run whatever the outcome. A trap that does
not call `exit` leaves the script's status as it was, so a red run stays red; a delete's failure is printed, never
fatal (`curl ... || true`).

**Alternatives considered**:

- *(reconstructed)* **Clean up at the end of the happy path.** Rejected: a failed run would leave everything behind.

---

## D3 - Bruno's teardown is a folder that runs last, and the order is explicit

**Decision**: `bruno/teardown/` with `seq: 17`; `bruno/seller/folder.yml` rewritten from `meta:` to `info:` with
`seq: 16`. Each delete accepts 204 or 404.

**Rationale**: Found while building: the seller folder ran last only because the CLI ignored its old-format `seq: 9`.
A teardown that did not know this ran before `seller`, deleted its category, and broke 19 requests. Making both numbers
explicit puts the order in the files rather than in an accident. 404 is accepted because Bruno's own
`DELETE /api/products/{id}` request may already have removed the product.

**Alternatives considered**:

- *(reconstructed)* **A post-run script outside Bruno.** Rejected: the variables that name what the run created live
  inside the run.

---

## D4 - Records of what happened stay

**Decision**: Customers, orders and addresses a run creates are not deleted.

**Rationale**: They are the records of what happened, and nothing lists them in a way that one run's leftovers can
break.

**Alternatives considered**:

- *(reconstructed)* **Delete everything a run created.** Rejected: there is no delete for an order, by design, and
  nothing breaks because of them.
