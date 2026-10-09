# Research: A gallery of photographs per product

## D1 - The cover stays on `products`; the rest go in a new table

**Decision**: `product_photos` holds the photographs **after** the cover. The cover keeps its three columns on
`products` and its derived key (specs/019).

**Rationale**: everything that shows a picture today - the listing, the cart, order rows, a variant's fallback
(specs/032), the image import (specs/079), the orphan report, the read cache - reads the cover from `products`. Keeping
it there changes none of them, and an older image after a rollback still shows the cover (expand-only, FR-010).

**Rejected**: moving every photograph, cover included, into the new table and serving the cover from it. One list is
tidier, but every reader of the cover would change at once, and an older image would read `products` columns nobody
writes any more: the cover frozen at whatever it was before the upgrade.

## D2 - A photograph's key is stored, not derived

**Decision**: `product_photos.StorageKey` holds the file's key. A photograph uploaded into the gallery gets
`photo-{id:N}-{ticks}.{ext}`; the filesystem store's key pattern accepts the `photo-` prefix.

**Rationale**: making a photograph the cover (D3) moves the old cover into the gallery **without copying its bytes** -
its file keeps the cover's key form (`{productId:N}-{ticks}.{ext}`), so the row must say which file it is. The prefix
keeps a gallery key apart from a product's and a variant's (specs/032's lesson: the first variant reuses the product's
id).

**Rejected**: deriving the key from the row's id and time like the cover and variants do. It would force a copy of the
old cover's bytes on every cover change, for no gain.

## D3 - Making a photograph the cover: write, switch, delete

**Decision**:
1. Read the chosen photograph's bytes and **write** them under a new cover key (`{productId}-{now}`).
2. In **one transaction**: switch the product's cover columns with the existing guarded statement (only if the cover is
   still the one read), delete the chosen photograph's row, insert the old cover as a photograph at the chosen one's
   position with its own key (none when there was no cover), record the audit entry and the review resubmission.
3. **Delete** the chosen photograph's old file.

Removing the cover when photographs remain is the same with the first photograph chosen and the old cover not kept -
its file deleted after the commit.

**Rationale**: the order specs/019 uses everywhere - at every moment every row names a file that exists; a failure
leaves at worst an orphan the report finds. The guarded switch makes a concurrent cover change lose cleanly (409, its
written file deleted).

**Rejected**: renaming in the store (S3 has no rename; a copy plus delete is what it would be anyway); keeping the cover
in the table and only flipping positions (D1).

## D4 - Ten photographs, the cover counted

**Decision**: `ProductGallery.MaxPhotos = 10` - the cover plus 9. Adding to a product with no cover makes the new
photograph the cover (the existing cover path), so a gallery never has photographs without a cover.

**Rationale**: enough for a listing (marketplaces commonly allow 8-12), and it bounds a moderator's review and the page.

## D5 - Every gallery change sends a seller's approved product back to review

**Decision**: add, remove, reorder and cover change all call `ProductReview.AfterSellerEditAsync`.

**Rationale**: specs/045's rule, decided with the user, is "any photograph"; removing the cover already does it. A
reorder introduces no new picture but changes which one the listing shows when it involves the cover, and one rule
for the gallery is simpler to say and to test than four. Staff changes do not resubmit (the function's own rule).

## D6 - Serving

**Decision**: `GET /api/products/{id}/photos/{photoId}?k=`. A photograph's bytes never change (a changed photograph is
a new row), so on a product on sale it is cacheable for good; off the shelf it is served only with its own `k`
(specs/081) and `private, no-store`, like the cover.

## D7 - Responses

**Decision**: `ProductResponse.Photos: [{ id, url }]` in order, filled by the lookup and by the review queue; `null` on
the listing.

**Rationale**: the listing shows the cover only (FR-003), and a page of 12 products should not carry 108 addresses. The
review queue needs them (FR-008).

## D8 - Reorder by a complete list

**Decision**: `PUT /api/products/{id}/photos/order` with every photograph id after the cover, each once; positions are
rewritten 0..n-1. A list that is not exactly the current set is 400 and changes nothing.

**Rationale**: a complete list cannot leave two photographs at one position or one forgotten, and a stale list (a
photograph added meanwhile) is refused rather than half-applied.

**Rejected**: "move one up/down" endpoints - several requests for one drag, each racing the others.
