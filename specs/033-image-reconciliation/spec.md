# Feature Specification: Finding the images nobody can name

> Completed on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature branch**: `033-image-reconciliation`
**Created**: 2026-09-23
**Status**: Merged (#74, 2026-09-23)
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

## User Scenarios & Testing

### US1 - Somebody asks how much is being wasted (Priority: P1)

An administrator asks what the store holds that the catalogue cannot name.

**Why this priority**: it is the question issue #67 asks, and it is useful on its own - knowing there
are three orphans is enough to decide to do nothing (research D2).

**Independent Test**: make an orphan the way the deliberate swallow makes one, ask for the report, and
find it named with nothing live beside it.

**Acceptance Scenarios**:

1. **Given** the store holds files, **When** an administrator asks, **Then** the answer lists each
   orphan's key, size and age, and the total.
2. **Given** a live product or variant image, **When** the report runs, **Then** it names **nothing**
   that a live product or variant currently points at.
3. **Given** a file written within the grace period, **When** the report runs, **Then** it is not named.
4. **Given** any report, **When** it has run, **Then** it has changed nothing.
5. **Given** a caller who is not an administrator, **When** they ask, **Then** they are refused.

---

### US2 - Somebody reclaims the space (Priority: P1)

**Why this priority**: small once the report exists, and its guards are the report's guards; but it is
the step that can destroy data, so it is its own story and its own request.

**Independent Test**: after the report names an orphan, send the reclaim and see exactly that orphan
removed and every live image still served.

**Acceptance Scenarios**:

1. **Given** the report named orphans, **When** the administrator reclaims, **Then** deleting removes exactly
   what the report named — strictly, what its own reconciliation finds at that moment, which is the
   same set unless something changed in between — and answers with what it removed.
2. **Given** a report was asked for, **When** nothing else is sent, **Then** nothing is deleted - the
   reclaim is a separate request.
3. **Given** a row changes between the listing and the delete, **When** the reclaim runs, **Then** a
   live image is never removed, because it reconciles again rather than trusting the earlier list.

---

### US3 - It refuses to guess (Priority: P1)

**Why this priority**: this is the failure that would delete the whole catalogue's images; without it
US1 and US2 are unsafe to ship.

**Independent Test**: make the catalogue read fail and ask for both the report and the reclaim; neither
names nor removes anything.

**Acceptance Scenarios**:

1. **Given** the catalogue cannot be read, **When** the report or the reclaim runs, **Then** **nothing
   is reported as an orphan and nothing is deleted** — an empty answer from the database must not be
   read as "everything is an orphan".
2. **Given** the store's own bookkeeping files, **When** the report runs, **Then** they are not orphans.

---

### Edge Cases

- **A file mid-upload.** Bytes written, row not yet switched: younger than the grace period, so not
  named (FR-003).
- **A failing database read.** Propagates; nothing reported, nothing deleted (FR-006).
- **The store's own bookkeeping.** `.write-probe-{guid}` and anything else starting with a dot is not
  listed at all.
- **A row changing between the report and the reclaim.** The reclaim reconciles again (FR-005).
- **One key the store will not delete.** Logged, reported in `failed`, and the rest still go.
- **Two Catalog instances.** Each would see only its own directory; the answer says so in a `note`
  rather than appearing to have solved it (Assumptions).

## Requirements

### Functional Requirements

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

### Key Entities

- **Stored image**: what the store holds under a key - its key, its size and its own last-modified
  time. Not a row anywhere; only the store knows it.
- **Live key**: a key some product or variant row currently names, derived from its id, its
  `ImageUpdatedAt` and its type (specs/019, 032).
- **Orphan**: a stored image whose key is not live and which is older than the grace period.

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
