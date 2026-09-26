# Feature Specification: Seed Photographs

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Feature Branch**: `030-product-photographs`

**Created**: 2026-09-23

**Status**: Merged as [#69](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/69) on 2026-09-23

**Input**: All 14 seeded cameras had `imageUrl: null`. The redesigned storefront (specs/025) drew a
deliberate placeholder for each, and the owner had offered to supply assets: "dropping files named by SKU
is all it would take".

## Context

Catalog could store and serve product images since specs/019, and replacing one deleted the old file
since specs/029. What was missing was a way to put real photographs on the development catalogue that
goes through the same path a person uses - and that does not put somebody else's photographs into a
**public** repository. The hard part of this feature is not uploading; it is where the pictures come
from and who may use them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A developer puts photographs on the seeded catalogue (Priority: P1)

A developer places one image per product in a local folder, named after the product's SKU, runs the
seeder, sees what it would do, then runs it again to upload.

**Why this priority**: It is the feature: a catalogue with pictures on it, through the API.

**Independent Test**: Put `CANON-R50.jpg` in `server/seed/images/`, run the seeder without and then with
`--yes`, and open the product in the storefront.

**Acceptance Scenarios**:

1. **Given** one file per product named by SKU, **When** the seeder runs without `--yes`, **Then** it lists
   what it would upload, to which product, the size, the type and whether it replaces an image, and
   uploads nothing.
2. **Given** the same files, **When** it runs with `--yes`, **Then** each is uploaded through
   `PUT /api/products/{id}/image` as an administrator and it reports uploaded, skipped and failed counts.
3. **Given** products with no matching file, **When** it finishes, **Then** it lists them.
4. **Given** a file whose name matches no SKU, **When** it runs, **Then** that file is skipped and said so.

---

### User Story 2 - The script refuses what Catalog would refuse, before sending it (Priority: P2)

A file that is not a JPEG, PNG or WebP by its bytes, or is over 2 MB, is named and refused locally rather
than posted.

**Why this priority**: The server refuses these anyway (specs/019); refusing them here gives a clearer
message and a correct count.

**Independent Test**: Rename an SVG to `.png` and add a 3 MB JPEG; run the seeder.

**Acceptance Scenarios**:

1. **Given** an SVG renamed `.png`, **When** the seeder runs, **Then** it reports "not a JPEG, PNG or WebP
   by its bytes" and counts it failed.
2. **Given** a `.jpg` that holds a PNG, **When** the seeder runs, **Then** it uploads - as the server
   accepts it.
3. **Given** a file over 2 MB, **When** the seeder runs, **Then** it names the size and Catalog's limit.

---

### User Story 3 - Nobody's photograph is committed, and every credit is written down (Priority: P1)

The images never enter the repository; where each came from and under what licence is committed instead.

**Why this priority**: The repository is public and a product photograph belongs to whoever took it.
Getting this wrong is not a bug that can be reverted: a commit is published.

**Independent Test**: `git check-ignore server/seed/images/SONY-A7M4.jpg` reports the path; each seeded SKU
has a row in `IMAGE-CREDITS.md`.

**Acceptance Scenarios**:

1. **Given** files in `server/seed/images/`, **When** `git status` runs, **Then** none is listed.
2. **Given** the photographs used on the development catalogue, **When** `IMAGE-CREDITS.md` is read,
   **Then** each SKU names its file, author and licence, or says it is an unlicensed local placeholder.

---

### Edge Cases

- **Re-running.** Uploading again replaces the image; Catalog writes the new file, switches the row and
  deletes the old one (specs/019, 029), so a re-run never leaves a product pointing at nothing and leaves
  no second file behind.
- **A search result that is the wrong thing.** Searching Commons for the Panasonic returned
  `Harbor rope.jpg`; for the X100VI, the 2011 X100 - a different camera. A file name is not evidence.
- **The previous generation.** A matching-style photograph of the ZV-E10 exists; not of the ZV-E10 II.
  Showing the older camera under this name would be quietly wrong.
- **No licensed photograph exists** (`FUJI-X100VI`). A local placeholder, recorded as not licensed for
  reuse.
- **No `ADMIN_EMAIL`/`ADMIN_PASSWORD`**, or no `seed/images/` folder: the script says so and exits 2.
- **Empty folder.** It says how to name files and exits 0.
- **A catalogue of more than 200 products.** The script reads one page of 200; products beyond it are not
  matched. The development catalogue has 14.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The seeder MUST match an image to a product by file name equal to the product's SKU, ignoring
  case and extension.
- **FR-002**: The seeder MUST upload through the same authenticated endpoint a person uses, as an
  administrator.
- **FR-003**: The seeder MUST do nothing without explicit confirmation, and MUST report what it would do.
- **FR-004**: The seeder MUST decide an image's type from its bytes and refuse anything that is not JPEG,
  PNG or WebP, or is over 2 MB, before sending it.
- **FR-005**: The seeder MUST NOT fetch images from anywhere.
- **FR-006**: The staging folder MUST be ignored by git.
- **FR-007**: The attribution of every photograph used MUST be committed.
- **FR-008**: The seeder MUST report uploaded, skipped and failed counts, list products with no file, and
  exit non-zero when anything failed.

### Key Entities

- **Staged photograph**: a local file named by SKU, never committed.
- **Image credit**: SKU, source file, author and licence - committed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every seeded product has a photograph after one run. At the merge: `14 uploaded, 0 skipped,
  0 failed`.
- **SC-002**: Re-running leaves exactly one file per product on the volume. At the merge: 14 uploads over 11
  existing images left 14 files, not 25.
- **SC-003**: No image file is ever committed; every image used has a credit row.

## Assumptions

- The person running the seeder chooses the source and bears the licensing decision; the script takes no
  part in it.
- The development catalogue is the 14 cameras of `seed/cameras.json`.
- Catalog's rules (JPEG, PNG or WebP by content; at most 2 MB) are those of specs/019.

## Out of Scope

- Fetching or scraping images.
- Variant photographs (specs/032).
- Any server change.
- Automated tests of the script: none were written; its checks were exercised by running it.
