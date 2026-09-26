# Feature Specification: The picture follows the variant

> Completed on 2026-09-27, after the feature merged (#73), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature branch**: `032-variant-images`
**Created**: 2026-09-23
**Status**: Merged (#73, 2026-09-23)
**Closes**: #72

## What is wrong

Choosing a variant changes the price, the SKU, the stock and the option summary — and **not the
picture**. A shopper who clicks "Bạc" sees the black camera and reasonably concludes the click did
not register.

Nothing in the system knows a variant can look different from its product: `ProductVariant` has no
image column at all, all three image routes are `products/{id}/image`, and the variant chooser in
the storefront does not mention the picture.

## The design decision, made from the data

⚠️ **The image hangs on the VARIANT, not on the option value** — and that contradicts the first
proposal made when this was raised.

The argument for per-option-value is real: a product with Colour × Capacity would otherwise need the
same red photograph uploaded twice. The seeded catalogue disproves it:

```
Fujifilm X-T5  (3 variants)
   FUJI-XT5               Màu=Đen · Bộ=Chỉ thân máy
   FUJI-XT5-BODY-SILVER   Màu=Bạc · Bộ=Chỉ thân máy
   FUJI-XT5-1855          Bộ=Kèm ống kính 18-55mm · Màu=Đen
```

`FUJI-XT5` and `FUJI-XT5-1855` are **both black** and do not look the same: one has a lens mounted.
A photograph attached to the value "Đen" would show a bare body for the kit or a kit for the bare
body — wrong either way, and wrong invisibly.

**A variant is the exact thing being sold, so a variant is what a photograph is of.** The cost —
two genuinely identical-looking variants needing the same file twice — is tedious rather than
wrong, and it is bounded by the fallback below.

## User Scenarios & Testing

### US1 - The picture follows the choice (Priority: P1)

A shopper opens a camera, clicks "Bạc", and sees the silver one.

**Why this priority**: it is the defect in #72. Everything else in the feature exists so this can be
true.

**Independent Test**: give one variant of a product its own photograph, open the product page, click
each variant, and watch the picture change or fall back.

**Acceptance Scenarios**:

1. **Given** a variant with its own photograph, **When** it is chosen, **Then** that photograph shows.
2. **Given** a variant without one, **When** it is chosen, **Then** the **product's** photograph shows,
   not a blank and not the previous variant's.
3. **Given** the product page opens, **When** it opens on whatever it opens on today, **Then** it shows
   that variant's picture.
4. **Given** a product with no photograph at all, **When** it is shown, **Then** it still shows the
   aperture tile, as now.

---

### US2 - A seller photographs a variant (Priority: P1)

**Why this priority**: without a way to attach the photograph there is nothing for US1 to show; and
the write is a new route open to sellers, so its refusal belongs to the same story.

**Independent Test**: as a seller, upload, replace and remove a photograph on a variant of her own
product; as a second seller, try the same and be refused.

**Acceptance Scenarios**:

1. **Given** a seller's own product, **When** she uploads, replaces or removes a photograph on one of
   its variants, **Then** it is accepted.
2. **Given** somebody else's variant, **When** a seller writes its photograph, **Then** it is **404**,
   never 403.
3. **Given** any upload, **When** it is checked, **Then** the type comes from the bytes, at most 2 MB —
   the same rules as a product image, refused the same way.
4. **Given** a replacement, **When** it runs, **Then** it writes the new file, switches the row, then
   deletes the old one, so the row never names a missing file.

---

### US3 - Nothing is left behind (Priority: P1)

**Why this priority**: specs/029 closed exactly this leak for the product's own image the day before;
reopening it would be a regression in the same week.

**Independent Test**: delete a product whose variants have photographs and list the store; nothing
of it is left.

**Acceptance Scenarios**:

1. **Given** a product with variant photographs, **When** it is deleted, **Then** **every** variant's
   photograph is deleted as well as its own.
2. **Given** the store fails while deleting, **When** that happens, **Then** the failure is logged and
   swallowed, exactly as specs/029 decided for the product's image.

---

### US4 - A listing card is unchanged (Priority: P2)

**Why this priority**: it is a thing not to change, stated so nobody "improves" it.

**Independent Test**: the catalogue grid shows the same picture before and after a variant is
photographed.

**Acceptance Scenarios**:

1. **Given** the catalogue grid, **When** a product's variants have photographs, **Then** the card still
   shows the product's own photograph. A card showing one arbitrary shape of the thing would be worse
   than what it shows now.

---

### Edge Cases

- **No image anywhere.** Neither the variant nor the product has one: `imageUrl` is null and the tile
  shows.
- **The reused first-variant id.** The first variant of a product has the product's id (specs/020), so
  the two keys must not rely on timestamps to differ (research D3).
- **A variant of a different product.** Named with the wrong product id in the path, it is the same 404
  as a missing variant.
- **Two replacements at once.** The switch is a guarded `UPDATE` on the version seen; the loser gets 409
  and its new file is deleted.
- **Removing a photograph that is not there.** Quiet success; the variant keeps falling back.
- **Deleting the product.** Every variant key is collected beside the product's and deleted after the
  row, a store failure logged and swallowed.

## Requirements

### Functional Requirements

- **FR-001** A variant MAY carry one image. Absence is normal and is not an error.
- **FR-002** A variant with no image MUST fall back to its product's, the way a missing translation
  falls back (specs/021).
- **FR-003** Image bytes MUST live behind `IProductImageStore`, with the key derived from the row
  and nothing about the image stored in a second place (specs/019 research D2).
- **FR-004** Writing a variant image MUST be refused for a product the caller does not own, with
  **404** and the same words as a variant that does not exist.
- **FR-005** Deleting a product MUST delete every variant image it had.
- **FR-006** The listing card MUST keep showing the product's image.
- **FR-007** Client changes ship with tests.

### Key Entities

- **Variant photograph**: at most one per variant, described by two columns on the variant — its type
  and the moment it was set, which is also its version. Its bytes live in the image store under a key
  derived from those two and the variant id.
- **Product photograph**: unchanged (specs/019); the fallback for a variant with none.

## Out of scope

- **A gallery.** Several images per variant is a different feature: ordering, a cover, removing one
  of many, and a changed read model on every product page.
- **Freezing an image onto an order line.** An order freezes the variant id, sku, name and option
  summary (specs/020) and has never frozen an image. A variant picture makes that absence more
  noticeable; it does not make it new.
- **Replacing the product image with "the first variant's".** Tempting — it removes the fallback —
  and wrong, see US4.

## Success Criteria

- **SC-001** Clicking each variant of a product that has variant photographs changes the picture,
  seen in a screenshot.
- **SC-002** A seller is refused another seller's variant image with 404, proven against the API
  with two real tokens.
- **SC-003** Deleting a product with variant images leaves nothing on the volume, proven by a test
  that fails without it.
- **SC-004** `verify-saga.sh` passes and the existing image tests still do.

## Assumptions

- One image per variant, matching one per product.
- The 14 seeded cameras keep working with no variant images at all, through the fallback.
