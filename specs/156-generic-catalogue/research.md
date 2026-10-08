# Research: A shop for anything, not only cameras

## D1. The model is already general; the work is around it

Read from the code:
- `ProductVariant`'s options are free text, on purpose ("a camera has kits, a shirt has sizes, a phone has storage").
- Categories are rows an administrator creates (`/admin/categories`), with an unused `ParentCategoryId`.
- Every camera word that reaches a person is in four locale strings and one SVG. Comments and test fixtures mention
  cameras as examples; they reach nobody and stay. So do the email and notice previews' sample product (`Fujifilm
  X-T5`): cameras remain one of the shop's verticals, so it is still a product the shop sells.

**Decision**: reword those strings and replace the SVG; no server change.

## D2. Wording

| Key | Before | After |
| :-- | :-- | :-- |
| `common.searchPlaceholder` | Search cameras… / Tìm máy ảnh… | Search products… / Tìm sản phẩm… |
| `catalog.hero.eyebrow` | Cameras / Máy ảnh | Many shops, one cart / Nhiều gian hàng, một giỏ hàng |
| `catalog.hero.heading` | Take the picture you are looking at. / Chụp đúng thứ bạn nhìn thấy. | Find the thing you came for. / Tìm đúng thứ bạn cần. |
| `seller.apply.subtitle` | Sell your own cameras and lenses here… / Bán máy ảnh và ống kính… | Sell your own goods here… / Bán sản phẩm của bạn tại đây… |
| `seller.addVariant.hint` | Another kit, colour or size… / Bộ, màu hoặc cỡ khác… | Another size, colour or model… / Cỡ, màu hoặc mẫu khác… |

**Rationale**: the hero's own rule is "everything in it is true" - "many shops, one cart" is what the marketplace is
(specs/027, one checkout across sellers), and claims nothing it cannot back.

## D3. The tile

**Decision**: a parcel (a box with its tape line), drawn inline like the aperture was, same stroke and tint.

**Alternatives rejected**: an "image" glyph (looks like a broken image, which the tile exists to avoid); a letter of
the name (the comment already rejects "a grey box with a letter").

## D4. Keeping it that way

A Vitest test reads the `common`, `catalog` and `seller` bundles in both languages and fails on `camera`, `lens`,
`máy ảnh`, `ống kính`. Narrow on purpose: those bundles are the shop's own sentences; product names come from the
catalogue, and fixtures may say anything.

## D5. One file per vertical, one loader

**Decision**: `server/seed/catalogue/<vertical>.json`, each in the existing format (`categories`, `products`), read by
`seed/catalogue.py`, which merges and validates them. The seeder and the cleaner import it, so "what the seed names"
has one definition - the cleaner deleting a vertical the seeder had just written would be the worst failure here.

**Validation** (each failure names the file): unique SKUs (products and variants, across files), unique category slugs,
a product's category exists, Vietnamese and English text on every category and product, every option a `[name,
value]` pair in both languages, the variants of one product naming the same options, a positive whole-dong price, a
positive dollar price with at most two decimals, whole non-negative stock.

**Run by CI** in the build job: `python3 seed/catalogue.py`. The seeder is not run in CI (it needs the whole stack);
the data it would send is checked instead.

**Alternatives rejected**: one big file (every edit conflicts, a vertical cannot be seeded alone); a directory of
products per category (more files than the format needs).

## D6. The verticals

Cameras (unchanged) plus: phones and laptops (storage, colour), clothing and shoes (size, colour), home and kitchen
(capacity, colour), books (cover), sports (size, weight). Real brands and models, descriptions written here, prices
approximate and stated so, stock modest. No photographs (D3's tile).
