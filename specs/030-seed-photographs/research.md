# Phase 0 Research: Seed Photographs

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-23

No separate design record was written while the feature was built; these decisions are reconstructed
from the script, the README, `IMAGE-CREDITS.md` and the pull request.

---

## D1 - The script fetches nothing

**Decision**: `seed-images.py` uploads what is already in `server/seed/images/`. It has no download step,
no source URL and no search.

**Rationale**: Choosing a source is a licensing decision, not a technical one, and it belongs to whoever
runs the script. A script that fetched would make that decision for every future user, silently, and
would be wrong the day a source changed its terms.

**Alternatives considered**:

- **Download from Wikimedia Commons by a list of URLs.** Rejected: bakes one person's licensing judgement
  into a tool, and a URL list is exactly how the wrong file (D4) would have been shipped.
- **Scrape retailer pages.** Rejected: not licensed for reuse.

---

## D2 - Images stay out of the repository; credits go in

**Decision**: `server/seed/images/` is gitignored, with a comment saying why. `server/seed/IMAGE-CREDITS.md`
is committed: for each SKU, the Commons file, the photographer and the licence; one row for the product
with no licensed photograph.

**Rationale**: The repository is public and a product photograph belongs to whoever took it. CC BY and
CC BY-SA require attribution, and "an attribution that exists only in somebody's shell history is not
one". The files go from the staging folder to the local image store and nowhere else.

**Alternatives considered**:

- **Commit the CC BY images.** Rejected: attribution would then be a condition on every fork and every
  copy of the repository, and the placeholder would be one careless `git add` away from being published.
- **Git LFS.** Rejected: still publishes them.

---

## D3 - Match by SKU, check by bytes, upload through the API

**Decision**: The file's stem, upper-cased, is looked up among the catalogue's SKUs. The type is decided
by magic bytes - JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A`, WebP `RIFF....WEBP` - and anything else,
or anything over 2 MB, is refused before sending. Upload is `PUT /api/products/{id}/image` with a multipart
part named `file`, as an administrator.

**Rationale**: A SKU is the one identifier a person can type as a file name and the seed data already
uses. Catalog decides the type from the bytes and never from the extension or `Content-Type` (specs/019),
so the script does too: a `.jpg` holding a PNG uploads, and an SVG renamed `.png` fails here rather than at
the server, with a clearer message and a correct count. Going through the endpoint means each image takes
the path a person's upload takes, including write-switch-delete on replacement.

**Alternatives considered**:

- **Trust the extension.** Rejected: disagrees with the server.
- **Copy files onto the volume and update rows with SQL.** Rejected: bypasses every rule in specs/019 and
  writes another service's database.
- **A mapping file from image to product.** Rejected: one more thing to keep in step when a file name can
  carry the SKU.

---

## D4 - Look at every photograph before using it

**Decision**: Every file was downloaded and looked at in a contact sheet before it was staged, and the
lesson is written into `IMAGE-CREDITS.md` for whoever re-runs this.

**Rationale**: "A file name is not evidence." Searching Commons for the Panasonic returned `Harbor rope.jpg`;
searching for the X100VI returned the 2011 X100 - a different camera. Either would have uploaded without
complaint.

**Alternatives considered**:

- **Trust the search result's title.** Rejected by the two results above.

---

## D5 - The right model over a matching set

**Decision**: 13 of 14 products use Wikimedia Commons studio photographs, almost all by Henry Söderlund,
so the grid reads as one shop. `SONY-ZVE10M2` uses a plainer photograph by a different author (PJ,
CC BY 4.0) because Commons has a Söderlund shot of the ZV-E10 but not of the ZV-E10 II. `FUJI-X100VI`,
which has no photograph on Commons, keeps a retailer's promotional image as a **local placeholder**,
recorded as not licensed for reuse.

**Rationale**: Showing the previous generation under this product's name would be quietly wrong - worse
than a set that does not quite match. The placeholder is visibly the odd card in the grid, "which is the
right amount of obvious".

**Alternatives considered**:

- **The ZV-E10 photograph for the ZV-E10 II.** Rejected: the wrong product.
- **The 2011 X100 for the X100VI.** Rejected: the wrong product.
- **No image for the X100VI.** Not recorded as considered; the placeholder was chosen and labelled.
