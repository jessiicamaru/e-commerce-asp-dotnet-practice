# Research: The picture follows the variant

## D1 - On the variant, not on the option value

**Decision**: the image hangs on the variant. **This reverses the first proposal made when the gap
was raised**, and the data is why.

Per-option-value is attractive: one "Đen" photograph shared by every black variant, so a
Colour × Capacity product needs it uploaded once rather than four times. The seeded catalogue
refutes it in a single product:

```
FUJI-XT5        Màu=Đen · Bộ=Chỉ thân máy
FUJI-XT5-1855   Bộ=Kèm ống kính 18-55mm · Màu=Đen
```

Both black. Visibly different — one has a lens on it. Attaching the picture to "Đen" shows a bare
body for the kit, or a kit for the bare body, and neither is detectable by looking at the data
model.

It would also need a rule for **which axis is the visual one**, which the system has no way to know:
`Màu` is visual, `Bộ` is visual, `Dung lượng` would not be, and nothing distinguishes them but
knowing what the words mean.

A variant is the exact sellable thing (specs/020). A photograph is of a thing. The duplication cost
is real and accepted; the fallback in D2 keeps it from being compulsory.

## D2 - Absence falls back to the product

**Decision**: a variant with no image shows its product's.

Requiring one per variant would leave all 14 seeded cameras showing nothing, and would make adding a
variant a two-step operation that fails visibly in between.

**Deliberately unlike a missing price.** specs/022 refuses to fall back across currencies, because
showing a dong price to a dollar shopper is a wrong *number* and the shop would be charging it. A
picture is not an assertion in the same way: the product's photograph is a true photograph of the
product, just not of this exact shape. The nearest precedent is per-field translation fallback
(specs/021), and this behaves like that.

## D3 - The storage key

**Decision**: reuse `ProductImageKey`, unchanged.

`For(Guid id, DateTime updatedAt, ImageFormat format)` is already generic over the id — it was
written taking a product id but never required one. A variant id produces a distinct key because ids
are distinct, so product images and variant images share a directory without colliding and without a
prefix scheme to maintain.

⚠️ **The reused-id trap applies here.** The first variant of a product REUSES the product's id
(specs/020). So `product.Id == variant.Id` for that variant, and a naive key would be the same
string for both. The version differs — `ImageUpdatedAt` is a separate column on each row — so the
keys differ in practice, but **only by accident of timing**: setting both in the same tick would
collide. The key therefore gains an explicit discriminator rather than relying on two clocks not
agreeing.

## D4 - What the address looks like

**Decision**: `/api/products/{productId}/variants/{variantId}/image`, mirroring the price and
translation routes which are already nested that way.

Rejected `/api/variants/{id}/image`: there is no `variants` controller, and adding one for a single
route would split "things you do to a listing" across two controllers whose authorization rules must
then agree.

## D5 - Deleting the product

**Decision**: `DeleteProductCommandHandler` collects every variant's key beside the product's and
deletes them all after the row, in the same try/catch.

specs/029 fixed exactly this leak for the product's own image and stated the ordering and the
swallow. Adding variant images without extending it would reintroduce the same defect the same
week — which is why FR-005 exists and has a test that fails without it.

The variant ids are **already collected before the delete** for `ProductDeletedEvent`; the keys ride
along in the same loop.

## D6 - No new `Vary`

**Decision**: none needed. Checked rather than assumed.

`Vary: Accept-Language` and `Vary: X-Currency` exist because the *same address* returns different
representations per header. A variant image address names one variant and returns one file
regardless of language or currency, so it varies on nothing. The existing `Cache-Control: immutable`
with the version in the query string is the whole caching story, as it already is for a product
image.

## D7 - The listing card keeps the product's image

**Decision**: unchanged, and stated so nobody "improves" it.

A grid card showing the first variant's photograph would show one arbitrary shape of the thing —
"Fujifilm X-T5" illustrated by whichever variant happened to sort first. The product image is the
one picture that is about the product rather than about a shape of it.

## D8 - The cart and the order line

**Decision**: unchanged, and recorded rather than fixed.

An order freezes the variant id, sku, product name and option summary (specs/020) and has never
frozen an image. A variant photograph makes that absence more noticeable — an order page for a
deleted product already loses its picture (specs/029 research D1) — but it does not make it new, and
freezing images onto order lines is a storage decision belonging to whoever decides an order is a
receipt with a photograph on it.
