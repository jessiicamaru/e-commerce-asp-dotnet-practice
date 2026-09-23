# Implementation Plan: Finding the images nobody can name

**Branch**: `033-image-reconciliation` | **Spec**: [spec.md](spec.md) | **Closes**: #67

## Technical Context

One new capability on `IProductImageStore`, one query that reconciles, one command that reclaims,
two Admin routes, and a configurable grace period.

**No migration. No new table. No new message. No client change.**

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Untouched. Catalog reconciles its own store against its own rows. |
| II - Clean Architecture | Listing is added to the Application-layer interface; the handler never sees a directory. |
| III - Atomic writes, idempotent messaging | Nothing is written and nothing is published. The delete is idempotent by construction - a key already gone is not an error, which `DeleteAsync` already promises. |
| IV - Identity from the token | Admin only, decided by the attribute. There is no ownership to check: an orphan's row is gone, so nothing can say whose it was. |
| V - Evidence over assumption | Every dangerous case is shown **red before its guard exists**: a live product image, a live variant image, a file written seconds ago, and a failing database read. |

No Complexity Tracking entries.

## The one defect that matters more than the feature

⚠️ **An empty live-key set must never mean "everything is an orphan".**

If the repository read throws and the code carries on with nothing, every file in the store becomes
a candidate and the delete removes the entire catalogue's images. Nothing about that is
recoverable.

So the live keys are read **first**, a failure propagates, and a test asserts that a failing
repository yields nothing reported and nothing deleted. That test is written before the reconciler
is, and it is the reason the reconciler reads in that order rather than the other.

Two more that are nearly as bad, both guarded and both tested red first:

- **A file written seconds ago is not an orphan.** An upload between its two steps has written bytes
  no row names *yet*.
- **The delete must not trust the caller's key list.** It re-reconciles and removes what it finds;
  the report is advice, not an instruction.

## Phases

**Phase 1 - the store can be listed.** `ListAsync` streaming `StoredImage`, excluding the store's
own dotfiles. A test that it finds what was saved and ignores a probe file.

**Phase 2 - the live keys.** A repository read returning every key a product or variant currently
names. This is the set everything else is compared against, so it is tested for both kinds of key.

**Phase 3 - the reconciler, dangerous cases first.** Four tests seen red, then
`FindOrphanImagesQuery`.

**Phase 4 - reclaiming.** `RemoveOrphanImagesCommand`, re-reconciling rather than taking keys.

**Phase 5 - the routes.** Two, `Admin` only. A test that a seller is refused.

**Phase 6 - end to end.** An orphan made the way the deliberate swallow makes one, found, and
removed; `verify-saga.sh`; Bruno.

**Phase 7 - say so.** CLAUDE.md, and the gotcha about what the one-instance assumption now costs.

## Verification

- Catalog 122 to about 132.
- The four dangerous-case tests **must be seen failing** before their guards.
- On the running stack: create an orphan, report it, reclaim it, and confirm the fourteen real
  camera images are untouched throughout.
- `verify-saga.sh` and Bruno.
