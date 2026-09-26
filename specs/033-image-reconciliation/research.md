# Research: Finding the images nobody can name

> Completed on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md. Decisions as written before the code; **Rationale** and **Alternatives
> considered** labels added so each reads like specs/001.

**Feature**: [spec.md](spec.md)

## D1 - Two endpoints, not a background service

**Decision**: `GET /api/products/images/orphans` reports, `DELETE /api/products/images/orphans`
reclaims. Administrators only. Nothing runs on a timer.

**Rationale**: The obvious design is a hosted service in Catalog on a schedule — the expiry sweeper in Inventory is
exactly that, and it works. **Rejected here, and the difference is what the two sweepers do when
they are wrong.** Inventory's returns held stock to the shelf; being early is a lost sale, being
late is a delayed one, and both are recoverable. This one **deletes bytes nobody can recreate**. A
schedule means it runs unattended, and the only failure that matters is the one nobody is watching.

A hosted service would also run on **every** instance, and specs/019's one-instance assumption means
two instances each see only their own directory — so the second would compute a difference against
the first's rows and be wrong about every file.

**An endpoint puts a person in the loop, which for this operation is the feature rather than the
friction.**

**Alternatives considered**: a hosted service on a schedule, like Inventory's expiry sweeper -
rejected above; a startup hook - rejected for the same reason (no startup hook exists, per
[contracts/api.md](contracts/api.md)).

## D2 - Reporting stands on its own

**Decision**: the report is complete and useful without the delete, and shipping only it would have
been an acceptable outcome.

**Rationale**: Knowing there are three orphans totalling 400 KB is enough to decide to do nothing. Knowing there
are nine thousand is what justifies the risk of deleting. The report answers the question the issue
actually asks — *how much is being wasted* — and the delete is a second decision made with the
answer in hand.

Both are built here because the delete is small **once the report exists** and its guards are the
report's guards. But the order matters: the report is what is tested hardest.

**Alternatives considered**: shipping the report alone - acceptable, not chosen, because the delete
reuses the same scan (`OrphanImageScan`) and its guards.

## D3 - The grace period, and what it is actually protecting

**Decision**: configurable, `ProductImages:OrphanGraceHours`, default **24**.

**Rationale**: An upload writes the bytes, then switches the row. Between those two moments the file exists and no
row names it — it is indistinguishable from an orphan, and deleting it destroys an image that is
about to become live.

That window is milliseconds in the normal case, so almost any grace period covers it. **24 hours is
not chosen to cover the window; it is chosen to cover the operator.** A file created this morning
being deleted this afternoon is the kind of thing that turns out to have been a migration somebody
was in the middle of. The cost of waiting a day is bytes; the cost of not waiting is an image.

**Lower it deliberately, never silently** — which is why it is configuration and not a constant.

As built: `OrphanImageOptions.OrphanGraceHours` (default 24), bound from the `ProductImages` section in
Catalog's `Program.cs`, with `"OrphanGraceHours": 24` in `appsettings.json`.

**Alternatives considered**: a constant - rejected, lowering it must be deliberate; a grace sized to
the upload window (seconds) - rejected, the value covers the operator, not the window.

## D4 - The shape of the new listing capability

**Decision**: `IAsyncEnumerable<StoredImage> ListAsync(CancellationToken)`, where
`StoredImage` is `(string Key, long Size, DateTimeOffset LastModified)`.

**Rationale**: Streaming rather than `Task<List<...>>`, because `IProductImageStore` is the seam object storage
plugs into and a bucket listing is **paged**. A signature returning a whole list invites an
implementation that buys the whole bucket into memory, and the caller here does not need it — it
compares each key against a set and accumulates only the differences.

`LastModified` comes from the store rather than from the key, deliberately: the ticks in a key are
the *row's* version, and an orphan's row is gone. The file's own timestamp is the only thing that
can answer "how old is this".

**Alternatives considered**: `Task<List<StoredImage>>` - rejected above; age from the key's ticks -
rejected above.

## D5 - What is NOT an orphan

**Decision**: three exclusions, each for its own reason.

**Rationale**, one per exclusion:

- **A key a live row names.** The whole point.
- **Anything younger than the grace period.** D3.
- **Anything the store keeps for itself.** `FileSystemProductImageStore` writes
  `.write-probe-{guid}` at construction and deletes it; a crash at the wrong instant leaves one
  behind. It is not a product image and must not be counted as waste, let alone deleted as if the
  catalogue had lost it. The store excludes its own bookkeeping from its own listing, because it is
  the only thing that knows what that is. As built, `FileSystemProductImageStore.ListAsync` skips
  every name beginning with a dot.

**Alternatives considered**: filtering the probe in the scan rather than the store - rejected above,
only the store knows its own bookkeeping.

## D6 - An empty database read is not "everything is an orphan"

**Decision**: read the live keys **first**, and treat any failure as fatal to the whole operation.

**Rationale**: This is the single most dangerous defect available here. If the query throws and the code carries on
with an empty set, every file in the store becomes a candidate and the delete removes the entire
catalogue's images. Nothing about that is recoverable, and the report alone would still be alarming
enough to act on.

So: the keys are read before the store is listed, an exception propagates, and a test asserts that a
failing repository yields **nothing reported and nothing deleted** rather than everything.

As built, the scan depends on `ILiveImageKeys`, one method the repository also implements (tasks T024),
so the test can supply a catalogue that throws without implementing the whole repository.

**Alternatives considered**: catching the failure and returning an empty report - rejected, it is
the defect; listing the store first - rejected, the order is what the test pins.

## D7 - Re-checking at the moment of deletion

**Decision**: the delete recomputes the live set; it does not trust a key list from the caller.

**Rationale**: An endpoint that took "delete these keys" would be a request that deletes any file named in it, and
the caller's list is minutes old by the time somebody reads the report and decides. Between the two,
an upload can make one of those keys live.

The delete therefore performs the same reconciliation and removes what **it** finds, reporting what
it removed. The report is advice, not an instruction.

**Alternatives considered**: `DELETE` with a body of keys - rejected above.

## D8 - The one-instance assumption is inherited, not solved

**Decision**: say so in the response.

**Rationale**: With two Catalog instances the directory is per-instance and the rows are shared, so each instance
would see the other's images as orphans and delete them. That is catastrophic and entirely
plausible-looking.

specs/019 already records the assumption; this feature makes breaking it **destructive** rather than
merely broken, so the report carries a line naming it. A future object-storage implementation
removes the problem by making the store shared.

As built, the `note` was the constant `OrphanImageScan.OneInstanceNote`. specs/079 later moved the
images to a shared S3 bucket and drew the note from `IProductImageStore.SharedAcrossInstances`; that
is a later feature, not this one.

**Alternatives considered**: none recorded.
