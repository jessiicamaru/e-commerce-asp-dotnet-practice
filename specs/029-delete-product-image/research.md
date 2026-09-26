# Research: A deleted product takes its picture with it

> Completed on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

The decisions below were written before the change. On 2026-09-27 each was given explicit
**Rationale** and **Alternatives considered** headings; the wording under them is the original, and an
alternative added then says so.

## D1 - An order for a deleted product loses its picture. Is that acceptable?

**Decision**: yes, and it is not a new consequence of this change.

**Rationale**: An order freezes the name, price, sku and option summary of what was bought (specs/009, specs/020) -
it does **not** freeze the image, and never has. `DELETE /api/products/{id}/image` and a replacement
both already change what an order page shows today, and nothing in specs/019 or specs/024 claims
otherwise.

What this change does is make the *product* deletion behave like the other three paths instead of
being the one that quietly hoards bytes. Freezing a copy of the image onto every order line is a
real design with a real cost - storage per order line, a second copy to keep consistent, and a
decision about what an order page shows when the copy is missing - and it belongs to whoever decides
that an order is a receipt with a photograph on it. Not here.

**Alternatives considered**:

- **Rejected - keep the file so old orders keep their picture.** That is the behaviour being fixed,
described as a feature. Nothing reads those bytes: the only address that serves them is
`GET /api/products/{id}/image`, which 404s once the product is gone. Bytes no route can reach are
not a fallback, they are a leak.

## D2 - Row first or bytes first?

**Decision**: row first, bytes second - the order `UploadProductImageCommand` and
`RemoveProductImageCommand` already use, for the reason they already state.

**Rationale**: Deleting the bytes first opens a window where a live row names a file that is gone: every request
for that image 404s while the product is still listed, and if the transaction then rolls back the
window never closes. Deleting them second opens a window where a file exists that no row names -
which is exactly the orphan state this feature is about, except bounded to the length of one
handler rather than to forever, and it is the state FR-003 already accepts permanently when the
store fails.

Asymmetric failures, and the cheap one is chosen on purpose.

**Alternatives considered**:

- **Bytes first.** Rejected for the window above: a live row naming a missing file, made permanent by a
  rollback.
- **Bytes inside the transaction.** Not possible: a file store does not take part in a PostgreSQL
  transaction, so "inside" would still mean one of the two orders above. (Added 2026-09-27.)

## D3 - What happens when the store throws?

**Decision**: log it with the key and carry on. The delete succeeds.

**Rationale**: `RemoveProductImageCommandHandler` already does exactly this, and its log line already says the
file "is left behind as an orphan". Copying the wording as well as the shape means one search finds
every place this can happen.

**Alternatives considered**:

- **Failing the delete.** The alternative - failing the delete - makes a leftover PNG able to stop an administrator removing
an embarrassing row from the catalogue, which is the operation specs/024 exists for. It also cannot
be retried into success if the store is genuinely read-only.

**This is a deliberate, bounded leak**, and it is what makes the sweeper in D4 eventually necessary
rather than merely tidy.

## D4 - Does a reconciliation sweeper belong in this change?

**Decision**: no. Separate issue, and this spec says so out loud rather than leaving it implied.

**Rationale**: A sweeper lists the store, asks the database which keys are current, and deletes the difference. It
is the only thing that can recover what is already orphaned - including the two files from #66, the
ones FR-003 will create in future, and anything lost to a crash between the two steps.

It is also the only piece of this that can delete a **live** product's image. Get the query wrong,
or run it while an upload is between its two steps, and it destroys bytes a row is pointing at. That
risk deserves its own spec, its own tests and its own dry-run, not a paragraph at the end of a
four-line fix.

The two known orphans are deleted **by hand**, because two files is not a reason to build a robot.

**Alternatives considered**:

- **Build the sweeper here.** Rejected for the risk above. It became #67 and, as specs/033, an
  on-request report and reclaim that reads the live keys first and never runs on a timer.

## D5 - Does the key need reading before the row is removed?

**Decision**: yes, and for the same reason `variantIds` already is.

**Rationale**: `ProductImageKey.For(product)` reads `ImageUpdatedAt` and `ImageContentType` off the entity. After
`Remove` plus `SaveChangesAsync` the entity is detached and its values happen to still be in memory,
so reading it late would work today - and it would be working by accident, next to a comment
explaining why the variant ids are collected early. Reading both in the same place is the version
that survives somebody changing how the repository detaches.

**Alternatives considered**:

- **Read the key after `SaveChangesAsync`.** Works today, by accident, as described above.

## D6 - Is `DeleteAsync` on a key that is not there safe to call?

**Decision**: yes, stated by the interface: *"Removes the key. Removing what is not there is not an
error."*

**Rationale**: No existence check is needed, and adding one would be a second opinion about a promise the
seam already makes - and a race, since between the check and the delete nothing is holding anything.

**Alternatives considered**:

- **Check that the file exists, then delete.** Rejected above: a second opinion about the seam's promise,
  and a race.
