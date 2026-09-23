# Feature Specification: The picture follows the variant

**Feature branch**: `032-variant-images`
**Created**: 2026-09-23
**Status**: Draft
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

## User Scenarios

### US1 - The picture follows the choice (P1)

A shopper opens a camera, clicks "Bạc", and sees the silver one.

**Acceptance**
1. A variant with its own photograph shows it when chosen.
2. A variant without one shows the **product's** photograph, not a blank and not the previous
   variant's.
3. The product page opens on whatever it opens on today, with that variant's picture.
4. A product with no photograph at all still shows the aperture tile, as now.

### US2 - A seller photographs a variant (P1)

**Acceptance**
1. A seller uploads, replaces and removes a photograph on a variant of their own product.
2. Somebody else's variant is **404**, never 403.
3. The type comes from the bytes, at most 2 MB — the same rules as a product image, refused the
   same way.
4. Replacing writes the new file, switches the row, then deletes the old one, so the row never
   names a missing file.

### US3 - Nothing is left behind (P1)

**Acceptance**
1. Deleting a product deletes **every** variant's photograph as well as its own.
2. A store failure is logged and swallowed, exactly as specs/029 decided for the product's image.

### US4 - A listing card is unchanged (P2)

**Acceptance**
1. The catalogue grid still shows the product's own photograph. A card showing one arbitrary shape
   of the thing would be worse than what it shows now.

## Requirements

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
