# Feature Specification: Finding the images nobody can name

**Feature branch**: `033-image-reconciliation`
**Created**: 2026-09-23
**Status**: Draft
**Closes**: #67

## What is wrong

Nothing reconciles the image store against the catalogue. An image can become unreachable and stay
on the volume with nothing able to notice it, count it, or reclaim it.

specs/029 fixed the cause that was losing them in bulk, and specs/032 extended it to every variant's
photograph. **Three ways to orphan remain, and all three are deliberate:**

1. **A swallowed store failure.** Four handlers log `"it is left behind as an orphan"` and carry on,
   because a product that cannot be removed from the catalogue because of a leftover PNG is a worse
   defect than the leak.
2. **A crash between the two steps.** The row goes first and the bytes second, on purpose.
3. **A replacement whose old-file delete fails.** Same bargain.

None of these breaks anything, which is exactly why nobody will report them.

## Why only a reconciliation can find them

A key is derived from its row — `{productId:N}-{ticks}.{ext}`, or `variant-{variantId:N}-{ticks}.{ext}`
since specs/032. Once the row is gone, or its `ImageUpdatedAt` has moved, **nothing in the system can
name the old file**: no route serves it and no code reads it. Only something that lists the store and
asks the database which keys are current can see the difference.

## The shape this takes, and why it is not a background job

⚠️ **It never runs on a timer.** A sweeper that runs by itself is a sweeper that can delete a live
image at three in the morning with nobody watching. This is the one piece of the image feature whose
failure mode is *destroying data somebody is using*, and the mitigation is that a person asks for it.

⚠️ **Reporting comes first and stands on its own.** Counting orphans and naming them is useful
without deleting anything, and stopping there is a legitimate outcome rather than a half-finished
one. The deletion is a separate, explicit act.

## User Scenarios

### US1 - Somebody asks how much is being wasted (P1)

An administrator asks what the store holds that the catalogue cannot name.

**Acceptance**
1. The answer lists each orphan's key, size and age, and the total.
2. It names **nothing** that a live product or variant currently points at.
3. It names nothing written within the grace period.
4. It changes nothing.
5. Anyone who is not an administrator is refused.

### US2 - Somebody reclaims the space (P1)

**Acceptance**
1. Deleting removes exactly what the report named, and answers with what it removed.
2. It is a separate request. Nothing deletes as a side effect of asking.
3. A live image is never removed, even if a row changes between the listing and the delete.

### US3 - It refuses to guess (P1)

**Acceptance**
1. If the catalogue cannot be read, **nothing is reported as an orphan and nothing is deleted** — an
   empty answer from the database must not be read as "everything is an orphan".
2. The store's own bookkeeping files are not orphans.

## Requirements

- **FR-001** The store MUST be able to list what it holds. It cannot today.
- **FR-002** A key currently named by a product or a variant MUST NOT be reported or deleted.
- **FR-003** A key younger than a configurable grace period MUST NOT be reported or deleted, because
  an upload between its two steps has written bytes no row names **yet**.
- **FR-004** Reporting MUST change nothing.
- **FR-005** Deleting MUST be a separate, explicit request, and MUST re-check FR-002 at the moment
  it deletes.
- **FR-006** A failure to read the catalogue MUST abort, reporting nothing and deleting nothing.
- **FR-007** Both MUST be administrators only.
- **FR-008** Nothing MUST run on a schedule.

## Out of scope

- **A background service or a cron.** FR-008; see the reasoning above.
- **Object storage.** `IProductImageStore` is the seam, and the listing added here must be shaped so
  a paged, eventually-consistent, charged-for implementation can satisfy it — but no such
  implementation is written here.
- **Repairing a row that names a missing file.** That is the opposite direction and a different
  problem: the row is wrong, not the store. Already logged at Error where it can happen.

## Success Criteria

- **SC-001** An orphan created by the deliberate swallow is found and named.
- **SC-002** A live product image and a live variant image are never named, proven by tests that
  fail without the guard.
- **SC-003** An image written seconds ago is never named.
- **SC-004** A database failure yields nothing reported and nothing deleted.
- **SC-005** A non-administrator is refused.

## Assumptions

- One Catalog instance, as specs/019 already assumes. With two, each sees only its own directory and
  a reconciliation on one says nothing about the other. This inherits that assumption and must say
  so out loud rather than appear to have solved it.
- Orphans are rare today. This is instrumentation for a leak that is bounded and known, not a
  response to a flood.
